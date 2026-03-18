using JiraClone.Application.Abstractions;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.ActivityLog;

public class ActivityLogService
{
    private readonly IActivityLogRepository _activityLogs;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(IActivityLogRepository activityLogs, ILogger<ActivityLogService>? logger = null)
    {
        _activityLogs = activityLogs;
        _logger = logger ?? NullLogger<ActivityLogService>.Instance;
    }

    public Task<IReadOnlyList<ActivityLogEntity>> GetIssueActivityAsync(int issueId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading issue activity for issue {IssueId}.", issueId);
        return _activityLogs.GetIssueActivityAsync(issueId, cancellationToken);
    }

    public Task<IReadOnlyList<ActivityLogEntity>> GetProjectActivityAsync(int projectId, int take = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading project activity for project {ProjectId} with take {Take}.", projectId, take);
        return _activityLogs.GetProjectActivityAsync(projectId, take, cancellationToken);
    }
}
