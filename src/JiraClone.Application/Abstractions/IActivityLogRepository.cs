using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Application.Abstractions;

public interface IActivityLogRepository
{
    Task<IReadOnlyList<ActivityLogEntity>> GetIssueActivityAsync(int issueId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityLogEntity>> GetProjectActivityAsync(int projectId, int take = 10, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityLogEntity>> GetProjectStatusChangesAsync(int projectId, CancellationToken cancellationToken = default);
    Task AddAsync(ActivityLogEntity activityLog, CancellationToken cancellationToken = default);
}