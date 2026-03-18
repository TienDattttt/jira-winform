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
    private readonly IUserRepository _users;
    private readonly IIssueRepository _issues;
    private readonly IProjectRepository _projects;
    private readonly INotificationEmailTemplateRenderer _templateRenderer;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notifications,
        IUserRepository users,
        IIssueRepository issues,
        IProjectRepository projects,
        INotificationEmailTemplateRenderer templateRenderer,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        ILogger<NotificationService>? logger = null)
    {
        _notifications = notifications;
        _users = users;
        _issues = issues;
        _projects = projects;
        _templateRenderer = templateRenderer;
        _emailService = emailService;
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

        var emailRequest = await BuildEmailRequestAsync(notification, cancellationToken);
        if (emailRequest is not null)
        {
            _ = _emailService
                .SendAsync(emailRequest.ToEmail, emailRequest.ToName, emailRequest.Subject, emailRequest.HtmlBody, CancellationToken.None)
                .ContinueWith(
                    task => _logger.LogError(task.Exception, "Email send failed for notification {NotificationId}.", notification.Id),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
        }

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

    private async Task<EmailDeliveryRequest?> BuildEmailRequestAsync(Notification notification, CancellationToken cancellationToken)
    {
        var recipient = await _users.GetByIdAsync(notification.RecipientUserId, cancellationToken);
        if (recipient is null ||
            !recipient.IsActive ||
            !recipient.EmailNotificationsEnabled ||
            string.IsNullOrWhiteSpace(recipient.Email))
        {
            return null;
        }

        Issue? issue = null;
        if (notification.IssueId.HasValue)
        {
            issue = await _issues.GetByIdAsync(notification.IssueId.Value, cancellationToken);
        }

        Project? project = null;
        if (notification.ProjectId.HasValue)
        {
            project = await _projects.GetByIdAsync(notification.ProjectId.Value, cancellationToken);
        }

        if (project is null && issue is not null)
        {
            project = await _projects.GetByIdAsync(issue.ProjectId, cancellationToken);
        }

        var templateModel = new NotificationEmailTemplateModel(
            notification.Type,
            string.IsNullOrWhiteSpace(recipient.DisplayName) ? recipient.UserName : recipient.DisplayName,
            notification.Title,
            notification.Body,
            issue?.IssueKey,
            issue?.Title,
            project?.Name,
            ExtractSprintName(notification));

        return new EmailDeliveryRequest(
            recipient.Email,
            string.IsNullOrWhiteSpace(recipient.DisplayName) ? recipient.UserName : recipient.DisplayName,
            notification.Title,
            _templateRenderer.Render(templateModel));
    }

    private static string? ExtractSprintName(Notification notification)
    {
        const string startedPrefix = "Sprint started:";
        const string completedPrefix = "Sprint completed:";
        if (notification.Title.StartsWith(startedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return notification.Title[startedPrefix.Length..].Trim();
        }

        if (notification.Title.StartsWith(completedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return notification.Title[completedPrefix.Length..].Trim();
        }

        return notification.Title;
    }

    private static NotificationItemDto Map(Notification notification) =>
        new(notification.Id, notification.RecipientUserId, notification.IssueId, notification.ProjectId, notification.Type, notification.Title, notification.Body, notification.IsRead, notification.CreatedAtUtc);

    private sealed record EmailDeliveryRequest(string ToEmail, string ToName, string Subject, string HtmlBody);
}
