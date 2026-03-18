using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class WebhookEndpointRepository : IWebhookEndpointRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public WebhookEndpointRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<WebhookEndpoint>> GetByProjectIdAsync(int projectId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.WebhookEndpoints
            .Include(x => x.Subscriptions)
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WebhookEndpoint>> GetActiveByProjectAndEventAsync(int projectId, WebhookEventType eventType, CancellationToken cancellationToken = default)
    {
        return await _dbContext.WebhookEndpoints
            .Include(x => x.Subscriptions)
            .Where(x => x.ProjectId == projectId && x.IsActive && x.Subscriptions.Any(subscription => subscription.EventType == eventType))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<WebhookEndpoint?> GetByIdAsync(int endpointId, CancellationToken cancellationToken = default)
    {
        return _dbContext.WebhookEndpoints
            .Include(x => x.Subscriptions)
            .FirstOrDefaultAsync(x => x.Id == endpointId, cancellationToken);
    }

    public Task AddAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        return _dbContext.WebhookEndpoints.AddAsync(endpoint, cancellationToken).AsTask();
    }

    public Task RemoveAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        _dbContext.WebhookEndpoints.Remove(endpoint);
        return Task.CompletedTask;
    }
}