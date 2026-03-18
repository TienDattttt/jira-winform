using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(int notificationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetRecentByUserAsync(int userId, int take = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetUnreadByUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
}
