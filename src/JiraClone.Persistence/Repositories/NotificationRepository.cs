using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public NotificationRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Notification?> GetByIdAsync(int notificationId, CancellationToken cancellationToken = default) =>
        _dbContext.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId, cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetRecentByUserAsync(int userId, int take = 20, CancellationToken cancellationToken = default) =>
        await _dbContext.Notifications
            .Where(x => x.RecipientUserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetUnreadByUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await _dbContext.Notifications
            .Where(x => x.RecipientUserId == userId && !x.IsRead)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default) =>
        _dbContext.Notifications.CountAsync(x => x.RecipientUserId == userId && !x.IsRead, cancellationToken);

    public Task AddAsync(Notification notification, CancellationToken cancellationToken = default) =>
        _dbContext.Notifications.AddAsync(notification, cancellationToken).AsTask();
}
