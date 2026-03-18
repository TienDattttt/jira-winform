using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Abstractions;

public interface IWebhookService
{
    Task<IReadOnlyList<WebhookEndpoint>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<WebhookEndpoint> CreateAsync(int projectId, string name, string url, string secret, bool isActive, IReadOnlyCollection<WebhookEventType> subscribedEvents, CancellationToken cancellationToken = default);
    Task<WebhookEndpoint?> UpdateAsync(int endpointId, string name, string url, string secret, bool isActive, IReadOnlyCollection<WebhookEventType> subscribedEvents, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int endpointId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookDelivery>> GetDeliveryHistoryAsync(int endpointId, int take = 50, CancellationToken cancellationToken = default);
    Task<WebhookDelivery?> SendTestAsync(int endpointId, CancellationToken cancellationToken = default);
}