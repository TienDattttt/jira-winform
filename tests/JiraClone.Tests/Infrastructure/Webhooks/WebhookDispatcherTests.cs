using System.Collections.Concurrent;
using System.Net;
using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using JiraClone.Infrastructure.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Tests.Infrastructure.Webhooks;

public class WebhookDispatcherTests
{
    [Fact]
    public async Task EnqueueDispatch_ProcessesDeliveryInBackground()
    {
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = new DispatcherHarness(async (_, cancellationToken) =>
        {
            requestStarted.TrySetResult();
            await releaseRequest.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var startedAt = DateTime.UtcNow;
        harness.Dispatcher.EnqueueDispatch(7, WebhookEventType.IssueCreated, new { Job = "background" });
        var elapsed = DateTime.UtcNow - startedAt;

        Assert.True(elapsed < TimeSpan.FromMilliseconds(100));
        await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Empty(harness.Store.GetDeliveries());

        releaseRequest.TrySetResult();
        await WaitUntilAsync(() => harness.Store.GetDeliveries().Count == 1);

        Assert.True(harness.Store.GetDeliveries()[0].Success);
    }

    [Fact]
    public async Task ProcessQueueAsync_EndpointTimeout_RetriesThreeTimes_AndContinues()
    {
        using var harness = new DispatcherHarness(
            async (request, cancellationToken) =>
            {
                var payload = await request.Content!.ReadAsStringAsync(cancellationToken);
                if (payload.Contains("slow-job", StringComparison.Ordinal))
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            },
            new WebhookDispatcherOptions
            {
                QueueCapacity = 10,
                MaxAttempts = 3,
                RequestTimeout = TimeSpan.FromMilliseconds(30),
                RetryDelays = [TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10)]
            });

        harness.Dispatcher.EnqueueDispatch(7, WebhookEventType.IssueUpdated, new { Job = "slow-job" });
        harness.Dispatcher.EnqueueDispatch(7, WebhookEventType.IssueUpdated, new { Job = "good-job" });

        await WaitUntilAsync(() => harness.Store.GetDeliveries().Count >= 4, TimeSpan.FromSeconds(4));
        var deliveries = harness.Store.GetDeliveries();

        Assert.Equal(3, deliveries.Count(x => !x.Success));
        Assert.Equal(1, deliveries.Count(x => x.Success));
        Assert.Contains("good-job", deliveries[^1].Payload, StringComparison.Ordinal);
        Assert.True(deliveries[^1].Success);
    }

    [Fact]
    public async Task EnqueueDispatch_WhenQueueIsFull_DropsOldestWithoutThrowing()
    {
        var firstRequestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRequestReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        using var harness = new DispatcherHarness(
            async (_, cancellationToken) =>
            {
                if (Interlocked.Increment(ref requestCount) == 1)
                {
                    firstRequestStarted.TrySetResult();
                    await firstRequestReleased.Task.WaitAsync(cancellationToken);
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            },
            new WebhookDispatcherOptions
            {
                QueueCapacity = 2,
                MaxAttempts = 1,
                RequestTimeout = TimeSpan.FromSeconds(1),
                RetryDelays = []
            });

        harness.Dispatcher.EnqueueDispatch(7, WebhookEventType.IssueUpdated, new { Job = "job-1" });
        await firstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        harness.Dispatcher.EnqueueDispatch(7, WebhookEventType.IssueUpdated, new { Job = "job-2" });
        harness.Dispatcher.EnqueueDispatch(7, WebhookEventType.IssueUpdated, new { Job = "job-3" });
        harness.Dispatcher.EnqueueDispatch(7, WebhookEventType.IssueUpdated, new { Job = "job-4" });

        firstRequestReleased.TrySetResult();
        await WaitUntilAsync(() => harness.Store.GetDeliveries().Count == 3, TimeSpan.FromSeconds(3));
        var payloads = harness.Store.GetDeliveries().Select(x => x.Payload).ToArray();

        Assert.Contains(payloads, payload => payload.Contains("job-1", StringComparison.Ordinal));
        Assert.DoesNotContain(payloads, payload => payload.Contains("job-2", StringComparison.Ordinal));
        Assert.Contains(payloads, payload => payload.Contains("job-3", StringComparison.Ordinal));
        Assert.Contains(payloads, payload => payload.Contains("job-4", StringComparison.Ordinal));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var expiresAt = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(3));
        while (DateTime.UtcNow < expiresAt)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), "The expected condition was not satisfied before the timeout expired.");
    }

    private sealed class DispatcherHarness : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly HttpClient _httpClient;

        public DispatcherHarness(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
            WebhookDispatcherOptions? options = null)
        {
            Store = new InMemoryWebhookStore();
            Store.UpsertEndpoint(new WebhookEndpoint
            {
                Id = 10,
                ProjectId = 7,
                Name = "Automation Hook",
                Url = "https://example.test/webhook",
                Secret = "secret-value",
                IsActive = true,
                Subscriptions = [new WebhookEndpointSubscription { WebhookEndpointId = 10, EventType = WebhookEventType.IssueCreated }, new WebhookEndpointSubscription { WebhookEndpointId = 10, EventType = WebhookEventType.IssueUpdated }]
            });

            var services = new ServiceCollection();
            services.AddSingleton(Store);
            services.AddSingleton<IWebhookEndpointRepository, InMemoryWebhookEndpointRepository>();
            services.AddSingleton<IWebhookDeliveryRepository, InMemoryWebhookDeliveryRepository>();
            services.AddSingleton<IUnitOfWork, NoOpUnitOfWork>();
            services.AddSingleton<IWebhookSecretProtector, PassThroughWebhookSecretProtector>();
            _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            _httpClient = new HttpClient(new StubHttpMessageHandler(responder));
            Dispatcher = new WebhookDispatcher(
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                _httpClient,
                _serviceProvider.GetRequiredService<IWebhookSecretProtector>(),
                options ?? new WebhookDispatcherOptions(),
                NullLogger<WebhookDispatcher>.Instance);
        }

        public InMemoryWebhookStore Store { get; }

        public WebhookDispatcher Dispatcher { get; }

        public void Dispose()
        {
            Dispatcher.Dispose();
            _httpClient.Dispose();
            _serviceProvider.Dispose();
        }
    }

    private sealed class InMemoryWebhookStore
    {
        private readonly ConcurrentDictionary<int, WebhookEndpoint> _endpoints = new();
        private readonly object _deliveryGate = new();
        private readonly List<WebhookDelivery> _deliveries = [];

        public void UpsertEndpoint(WebhookEndpoint endpoint)
        {
            _endpoints[endpoint.Id] = CloneEndpoint(endpoint);
        }

        public IReadOnlyList<WebhookEndpoint> GetActiveEndpoints(int projectId, WebhookEventType eventType)
        {
            return _endpoints.Values
                .Where(endpoint => endpoint.ProjectId == projectId && endpoint.IsActive && endpoint.Subscriptions.Any(subscription => subscription.EventType == eventType))
                .Select(CloneEndpoint)
                .ToArray();
        }

        public WebhookEndpoint? GetEndpoint(int endpointId)
        {
            return _endpoints.TryGetValue(endpointId, out var endpoint)
                ? CloneEndpoint(endpoint)
                : null;
        }

        public void RemoveEndpoint(int endpointId)
        {
            _endpoints.TryRemove(endpointId, out _);
        }

        public void AddDelivery(WebhookDelivery delivery)
        {
            lock (_deliveryGate)
            {
                _deliveries.Add(CloneDelivery(delivery));
            }
        }

        public IReadOnlyList<WebhookDelivery> GetDeliveries()
        {
            lock (_deliveryGate)
            {
                return _deliveries.Select(CloneDelivery).ToArray();
            }
        }

        private static WebhookEndpoint CloneEndpoint(WebhookEndpoint endpoint)
        {
            return new WebhookEndpoint
            {
                Id = endpoint.Id,
                ProjectId = endpoint.ProjectId,
                Name = endpoint.Name,
                Url = endpoint.Url,
                Secret = endpoint.Secret,
                IsActive = endpoint.IsActive,
                CreatedAtUtc = endpoint.CreatedAtUtc,
                UpdatedAtUtc = endpoint.UpdatedAtUtc,
                Subscriptions = endpoint.Subscriptions
                    .Select(subscription => new WebhookEndpointSubscription
                    {
                        WebhookEndpointId = subscription.WebhookEndpointId,
                        EventType = subscription.EventType,
                    })
                    .ToList()
            };
        }

        private static WebhookDelivery CloneDelivery(WebhookDelivery delivery)
        {
            return new WebhookDelivery
            {
                Id = delivery.Id,
                WebhookEndpointId = delivery.WebhookEndpointId,
                EventType = delivery.EventType,
                Payload = delivery.Payload,
                ResponseCode = delivery.ResponseCode,
                Success = delivery.Success,
                AttemptedAtUtc = delivery.AttemptedAtUtc,
                RetryCount = delivery.RetryCount,
                ErrorMessage = delivery.ErrorMessage,
                CreatedAtUtc = delivery.CreatedAtUtc,
                UpdatedAtUtc = delivery.UpdatedAtUtc,
            };
        }
    }

    private sealed class InMemoryWebhookEndpointRepository(InMemoryWebhookStore store) : IWebhookEndpointRepository
    {
        public Task<IReadOnlyList<WebhookEndpoint>> GetByProjectIdAsync(int projectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WebhookEndpoint>>(store.GetActiveEndpoints(projectId, WebhookEventType.IssueCreated)
                .Concat(store.GetActiveEndpoints(projectId, WebhookEventType.IssueUpdated))
                .DistinctBy(endpoint => endpoint.Id)
                .OrderBy(endpoint => endpoint.Name)
                .ToArray());
        }

        public Task<IReadOnlyList<WebhookEndpoint>> GetActiveByProjectAndEventAsync(int projectId, WebhookEventType eventType, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(store.GetActiveEndpoints(projectId, eventType));
        }

        public Task<WebhookEndpoint?> GetByIdAsync(int endpointId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(store.GetEndpoint(endpointId));
        }

        public Task AddAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            store.UpsertEndpoint(endpoint);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            store.RemoveEndpoint(endpoint.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryWebhookDeliveryRepository(InMemoryWebhookStore store) : IWebhookDeliveryRepository
    {
        public Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
        {
            store.AddDelivery(delivery);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WebhookDelivery>> GetByEndpointIdAsync(int endpointId, int take = 50, CancellationToken cancellationToken = default)
        {
            var deliveries = store.GetDeliveries()
                .Where(delivery => delivery.WebhookEndpointId == endpointId)
                .OrderByDescending(delivery => delivery.AttemptedAtUtc)
                .Take(Math.Max(1, take))
                .ToArray();
            return Task.FromResult<IReadOnlyList<WebhookDelivery>>(deliveries);
        }

        public Task<WebhookDelivery?> GetLatestByEndpointIdAsync(int endpointId, CancellationToken cancellationToken = default)
        {
            var delivery = store.GetDeliveries()
                .Where(item => item.WebhookEndpointId == endpointId)
                .OrderByDescending(item => item.AttemptedAtUtc)
                .FirstOrDefault();
            return Task.FromResult(delivery);
        }
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class PassThroughWebhookSecretProtector : IWebhookSecretProtector
    {
        public string Protect(string secret) => secret;

        public string Unprotect(string protectedSecret) => protectedSecret;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request, cancellationToken);
    }
}

