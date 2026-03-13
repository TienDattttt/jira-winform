using JiraClone.Application.Abstractions;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Application.ActivityLog;

public class ActivityLogService
{
    private readonly IActivityLogRepository _activityLogs;

    public ActivityLogService(IActivityLogRepository activityLogs)
    {
        _activityLogs = activityLogs;
    }

    public Task<IReadOnlyList<ActivityLogEntity>> GetIssueActivityAsync(int issueId, CancellationToken cancellationToken = default) =>
        _activityLogs.GetIssueActivityAsync(issueId, cancellationToken);
}
