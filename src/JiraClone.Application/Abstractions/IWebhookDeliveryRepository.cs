using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IWebhookDeliveryRepository
{
    Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookDelivery>> GetByEndpointIdAsync(int endpointId, int take = 50, CancellationToken cancellationToken = default);
    Task<WebhookDelivery?> GetLatestByEndpointIdAsync(int endpointId, CancellationToken cancellationToken = default);
}