using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Abstractions;

public interface IWebhookDispatcher
{
    Task DispatchAsync(int projectId, WebhookEventType eventType, object payload, CancellationToken cancellationToken = default);
    Task<WebhookDelivery?> SendTestAsync(int endpointId, object payload, CancellationToken cancellationToken = default);
}