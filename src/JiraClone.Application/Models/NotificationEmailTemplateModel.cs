using JiraClone.Domain.Enums;

namespace JiraClone.Application.Models;

public sealed record NotificationEmailTemplateModel(
    NotificationType Type,
    string RecipientName,
    string Title,
    string Body,
    string? IssueKey,
    string? IssueTitle,
    string? ProjectName,
    string? SprintName);
