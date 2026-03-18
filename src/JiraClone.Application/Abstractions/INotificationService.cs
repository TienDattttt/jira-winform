using JiraClone.Application.Models;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Abstractions;

public interface INotificationService
{
    Task<NotificationItemDto> CreateNotificationAsync(int recipientUserId, NotificationType type, string title, string body, int? issueId = null, int? projectId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationItemDto>> GetUnreadAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationItemDto>> GetRecentAsync(int userId, int take = 20, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> MarkReadAsync(int notificationId, int userId, CancellationToken cancellationToken = default);
    Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken = default);
}
