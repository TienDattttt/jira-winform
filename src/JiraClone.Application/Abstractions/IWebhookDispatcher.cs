using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Abstractions;

public interface IWebhookDispatcher
{
    void EnqueueDispatch(int projectId, WebhookEventType eventType, object payload);
    Task<WebhookDelivery?> SendTestAsync(int endpointId, object payload, CancellationToken cancellationToken = default);
}
