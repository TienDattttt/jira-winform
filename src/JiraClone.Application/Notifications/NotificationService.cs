using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Notifications;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(INotificationRepository notifications, IUnitOfWork unitOfWork, ILogger<NotificationService>? logger = null)
    {
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<NotificationService>.Instance;
    }

    public async Task<NotificationItemDto> CreateNotificationAsync(int recipientUserId, NotificationType type, string title, string body, int? issueId = null, int? projectId = null, CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            RecipientUserId = recipientUserId,
            IssueId = issueId,
            ProjectId = projectId,
            Type = type,
            Title = title.Trim(),
            Body = body.Trim(),
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _notifications.AddAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created notification {NotificationType} for user {UserId}.", type, recipientUserId);
        return Map(notification);
    }

    public async Task<IReadOnlyList<NotificationItemDto>> GetUnreadAsync(int userId, CancellationToken cancellationToken = default) =>
        (await _notifications.GetUnreadByUserAsync(userId, cancellationToken)).Select(Map).ToList();

    public async Task<IReadOnlyList<NotificationItemDto>> GetRecentAsync(int userId, int take = 20, CancellationToken cancellationToken = default) =>
        (await _notifications.GetRecentByUserAsync(userId, take, cancellationToken)).Select(Map).ToList();

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default) =>
        _notifications.GetUnreadCountAsync(userId, cancellationToken);

    public async Task<bool> MarkReadAsync(int notificationId, int userId, CancellationToken cancellationToken = default)
    {
        var notification = await _notifications.GetByIdAsync(notificationId, cancellationToken);
        if (notification is null || notification.RecipientUserId != userId)
        {
            return false;
        }

        if (notification.IsRead)
        {
            return true;
        }

        notification.IsRead = true;
        notification.ReadAtUtc = DateTime.UtcNow;
        notification.UpdatedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _notifications.GetUnreadByUserAsync(userId, cancellationToken);
        var changed = 0;
        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            notification.UpdatedAtUtc = DateTime.UtcNow;
            changed++;
        }

        if (changed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }

    private static NotificationItemDto Map(Notification notification) =>
        new(notification.Id, notification.RecipientUserId, notification.IssueId, notification.ProjectId, notification.Type, notification.Title, notification.Body, notification.IsRead, notification.CreatedAtUtc);
}
