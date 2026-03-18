using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class WebhookDeliveryRepository : IWebhookDeliveryRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public WebhookDeliveryRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default)
    {
        return _dbContext.WebhookDeliveries.AddAsync(delivery, cancellationToken).AsTask();
    }

    public async Task<IReadOnlyList<WebhookDelivery>> GetByEndpointIdAsync(int endpointId, int take = 50, CancellationToken cancellationToken = default)
    {
        return await _dbContext.WebhookDeliveries
            .Where(x => x.WebhookEndpointId == endpointId)
            .OrderByDescending(x => x.AttemptedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);
    }

    public Task<WebhookDelivery?> GetLatestByEndpointIdAsync(int endpointId, CancellationToken cancellationToken = default)
    {
        return _dbContext.WebhookDeliveries
            .Where(x => x.WebhookEndpointId == endpointId)
            .OrderByDescending(x => x.AttemptedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}