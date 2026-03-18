using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Infrastructure.Webhooks;

public class WebhookDispatcher : IWebhookDispatcher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private readonly IWebhookEndpointRepository _endpoints;
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebhookDispatcher> _logger;

    public WebhookDispatcher(
        IWebhookEndpointRepository endpoints,
        IWebhookDeliveryRepository deliveries,
        IUnitOfWork unitOfWork,
        HttpClient httpClient,
        ILogger<WebhookDispatcher>? logger = null)
    {
        _endpoints = endpoints;
        _deliveries = deliveries;
        _unitOfWork = unitOfWork;
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<WebhookDispatcher>.Instance;
    }

    public async Task DispatchAsync(int projectId, WebhookEventType eventType, object payload, CancellationToken cancellationToken = default)
    {
        var endpoints = await _endpoints.GetActiveByProjectAndEventAsync(projectId, eventType, cancellationToken);
        if (endpoints.Count == 0)
        {
            return;
        }

        foreach (var endpoint in endpoints)
        {
            await SendToEndpointAsync(endpoint, eventType, payload, isTest: false, cancellationToken);
        }
    }

    public async Task<WebhookDelivery?> SendTestAsync(int endpointId, object payload, CancellationToken cancellationToken = default)
    {
        var endpoint = await _endpoints.GetByIdAsync(endpointId, cancellationToken);
        if (endpoint is null)
        {
            return null;
        }

        var eventType = endpoint.Subscriptions
            .Select(x => x.EventType)
            .DefaultIfEmpty(WebhookEventType.ProjectUpdated)
            .First();
        return await SendToEndpointAsync(endpoint, eventType, payload, isTest: true, cancellationToken);
    }

    private async Task<WebhookDelivery> SendToEndpointAsync(WebhookEndpoint endpoint, WebhookEventType eventType, object payload, bool isTest, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(
            new WebhookEnvelope(eventType, endpoint.ProjectId, DateTime.UtcNow, isTest, payload),
            SerializerOptions);

        WebhookDelivery? latestDelivery = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            latestDelivery = await ExecuteAttemptAsync(endpoint, eventType, body, attempt, cancellationToken);
            if (latestDelivery.Success)
            {
                break;
            }

            if (attempt < 3)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                await Task.Delay(delay, cancellationToken);
            }
        }

        return latestDelivery!;
    }

    private async Task<WebhookDelivery> ExecuteAttemptAsync(WebhookEndpoint endpoint, WebhookEventType eventType, string body, int attempt, CancellationToken cancellationToken)
    {
        var attemptedAtUtc = DateTime.UtcNow;
        var responseCode = 0;
        var success = false;
        string? errorMessage = null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("X-Jira-Desktop-Event", eventType.ToString());
            request.Headers.TryAddWithoutValidation("X-Jira-Desktop-Signature", ComputeSignature(endpoint.Secret, body));

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(10));
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
            errorMessage = "Request timed out after 10 seconds.";
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

        await _deliveries.AddAsync(delivery, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
}