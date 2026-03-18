using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Abstractions;

public interface IWebhookEndpointRepository
{
    Task<IReadOnlyList<WebhookEndpoint>> GetByProjectIdAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookEndpoint>> GetActiveByProjectAndEventAsync(int projectId, WebhookEventType eventType, CancellationToken cancellationToken = default);
    Task<WebhookEndpoint?> GetByIdAsync(int endpointId, CancellationToken cancellationToken = default);
    Task AddAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default);
    Task RemoveAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default);
}