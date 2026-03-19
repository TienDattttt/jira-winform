using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Infrastructure.Webhooks;

public sealed class WebhookDispatcher : IWebhookDispatcher, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _httpClient;
    private readonly IWebhookSecretProtector _secretProtector;
    private readonly WebhookDispatcherOptions _options;
    private readonly ILogger<WebhookDispatcher> _logger;
    private readonly CancellationTokenSource _shutdownSource = new();
    private readonly Channel<WebhookJob> _queue;
    private readonly Task _workerTask;
    private bool _disposed;

    public WebhookDispatcher(
        IServiceScopeFactory scopeFactory,
        HttpClient httpClient,
        IWebhookSecretProtector secretProtector,
        WebhookDispatcherOptions options,
        ILogger<WebhookDispatcher>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _httpClient = httpClient;
        _secretProtector = secretProtector;
        _options = options;
        _logger = logger ?? NullLogger<WebhookDispatcher>.Instance;
        _queue = Channel.CreateBounded<WebhookJob>(new BoundedChannelOptions(Math.Max(1, options.QueueCapacity))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
        _workerTask = Task.Run(ProcessQueueAsync);
    }

    public void EnqueueDispatch(int projectId, WebhookEventType eventType, object payload)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_queue.Writer.TryWrite(new WebhookJob(projectId, eventType, payload)))
        {
            _logger.LogWarning("Webhook queue rejected event {WebhookEventType} for project {ProjectId} because the dispatcher is shutting down.", eventType, projectId);
        }
    }

    public async Task<WebhookDelivery?> SendTestAsync(int endpointId, object payload, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var endpoints = scope.ServiceProvider.GetRequiredService<IWebhookEndpointRepository>();
        var deliveries = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var endpoint = await endpoints.GetByIdAsync(endpointId, cancellationToken);
        if (endpoint is null)
        {
            return null;
        }

        var eventType = endpoint.Subscriptions
            .Select(x => x.EventType)
            .DefaultIfEmpty(WebhookEventType.ProjectUpdated)
            .First();
        return await SendToEndpointAsync(endpoint, eventType, payload, isTest: true, deliveries, unitOfWork, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _queue.Writer.TryComplete();
        _shutdownSource.Cancel();

        try
        {
            _workerTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _shutdownSource.Dispose();
        }
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var job in _queue.Reader.ReadAllAsync(_shutdownSource.Token))
            {
                try
                {
                    await DeliverQueuedJobAsync(job, _shutdownSource.Token);
                }
                catch (OperationCanceledException) when (_shutdownSource.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Webhook dispatch job failed for project {ProjectId} and event {WebhookEventType}.", job.ProjectId, job.EventType);
                }
            }
        }
        catch (OperationCanceledException) when (_shutdownSource.IsCancellationRequested)
        {
        }
    }

    private async Task DeliverQueuedJobAsync(WebhookJob job, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var endpoints = scope.ServiceProvider.GetRequiredService<IWebhookEndpointRepository>();
        var deliveries = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var activeEndpoints = await endpoints.GetActiveByProjectAndEventAsync(job.ProjectId, job.EventType, cancellationToken);
        if (activeEndpoints.Count == 0)
        {
            return;
        }

        foreach (var endpoint in activeEndpoints)
        {
            await SendToEndpointAsync(endpoint, job.EventType, job.Payload, isTest: false, deliveries, unitOfWork, cancellationToken);
        }
    }

    private async Task<WebhookDelivery> SendToEndpointAsync(
        WebhookEndpoint endpoint,
        WebhookEventType eventType,
        object payload,
        bool isTest,
        IWebhookDeliveryRepository deliveries,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(
            new WebhookEnvelope(eventType, endpoint.ProjectId, DateTime.UtcNow, isTest, payload),
            SerializerOptions);

        WebhookDelivery? latestDelivery = null;
        var maxAttempts = Math.Max(1, _options.MaxAttempts);
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            latestDelivery = await ExecuteAttemptAsync(endpoint, eventType, body, attempt, deliveries, unitOfWork, cancellationToken);
            if (latestDelivery.Success)
            {
                break;
            }

            if (attempt < maxAttempts)
            {
                var delay = GetRetryDelay(attempt - 1);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        return latestDelivery!;
    }

    private async Task<WebhookDelivery> ExecuteAttemptAsync(
        WebhookEndpoint endpoint,
        WebhookEventType eventType,
        string body,
        int attempt,
        IWebhookDeliveryRepository deliveries,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        var attemptedAtUtc = DateTime.UtcNow;
        var responseCode = 0;
        var success = false;
        string? errorMessage = null;

        try
        {
            var secret = _secretProtector.Unprotect(endpoint.Secret);
            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new CryptographicException("Webhook secret could not be decrypted.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("X-Jira-Desktop-Event", eventType.ToString());
            request.Headers.TryAddWithoutValidation("X-Jira-Desktop-Signature", ComputeSignature(secret, body));

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_options.RequestTimeout);
            using var response = await _httpClient.SendAsync(request, timeoutSource.Token);

            responseCode = (int)response.StatusCode;
            success = response.IsSuccessStatusCode;
            if (!success)
            {
                errorMessage = $"{(int)response.StatusCode} {response.ReasonPhrase}";
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            errorMessage = $"Request timed out after {_options.RequestTimeout.TotalSeconds:0.#} seconds.";
        }
        catch (Exception exception)
        {
            errorMessage = exception.Message;
        }

        var delivery = new WebhookDelivery
        {
            WebhookEndpointId = endpoint.Id,
            EventType = eventType,
            Payload = body,
            ResponseCode = responseCode,
            Success = success,
            AttemptedAtUtc = attemptedAtUtc,
            RetryCount = attempt - 1,
            ErrorMessage = errorMessage,
            CreatedAtUtc = attemptedAtUtc,
            UpdatedAtUtc = attemptedAtUtc,
        };

        await deliveries.AddAsync(delivery, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (success)
        {
            _logger.LogInformation(
                "Webhook {WebhookEndpointId} delivered event {WebhookEventType} with status {StatusCode} on attempt {Attempt}.",
                endpoint.Id,
                eventType,
                responseCode,
                attempt);
        }
        else
        {
            _logger.LogWarning(
                "Webhook {WebhookEndpointId} failed event {WebhookEventType} on attempt {Attempt}: {ErrorMessage}",
                endpoint.Id,
                eventType,
                attempt,
                errorMessage);
        }

        return delivery;
    }

    private TimeSpan GetRetryDelay(int retryIndex)
    {
        if (_options.RetryDelays.Count == 0)
        {
            return TimeSpan.Zero;
        }

        return retryIndex >= 0 && retryIndex < _options.RetryDelays.Count
            ? _options.RetryDelays[retryIndex]
            : _options.RetryDelays[^1];
    }

    private static string ComputeSignature(string secret, string body)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        using var hmac = new HMACSHA256(secretBytes);
        return $"sha256={Convert.ToHexString(hmac.ComputeHash(bodyBytes)).ToLowerInvariant()}";
    }

    private sealed record WebhookEnvelope(
        WebhookEventType EventType,
        int ProjectId,
        DateTime SentAtUtc,
        bool IsTest,
        object Payload);

    private sealed record WebhookJob(int ProjectId, WebhookEventType EventType, object Payload);
}


