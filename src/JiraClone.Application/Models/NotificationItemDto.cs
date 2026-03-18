using JiraClone.Domain.Enums;

namespace JiraClone.Application.Models;

public sealed record NotificationItemDto(
    int Id,
    int RecipientUserId,
    int? IssueId,
    int? ProjectId,
    NotificationType Type,
    string Title,
    string Body,
    bool IsRead,
    DateTime CreatedAtUtc);
