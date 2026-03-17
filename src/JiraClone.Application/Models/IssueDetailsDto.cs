using JiraClone.Domain.Entities;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Application.Models;

public sealed record IssueDetailsDto(
    Issue Issue,
    IReadOnlyList<Comment> Comments,
    IReadOnlyList<Attachment> Attachments,
    IReadOnlyList<ActivityLogEntity> ActivityLogs,
    IReadOnlyList<Issue> SubIssues);
