using JiraClone.Application.Abstractions;
using JiraClone.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class ActivityLogRepository : IActivityLogRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public ActivityLogRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<JiraClone.Domain.Entities.ActivityLog>> GetIssueActivityAsync(int issueId, CancellationToken cancellationToken = default) =>
        await _dbContext.ActivityLogs
            .Include(x => x.User)
            .Where(x => x.IssueId == issueId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<JiraClone.Domain.Entities.ActivityLog>> GetProjectActivityAsync(int projectId, int take = 10, CancellationToken cancellationToken = default) =>
        await _dbContext.ActivityLogs
            .Include(x => x.User)
            .Include(x => x.Issue)
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<JiraClone.Domain.Entities.ActivityLog>> GetProjectStatusChangesAsync(int projectId, CancellationToken cancellationToken = default) =>
        await _dbContext.ActivityLogs
            .Where(x => x.ProjectId == projectId && x.IssueId != null && x.ActionType == ActivityActionType.StatusChanged)
            .OrderBy(x => x.IssueId)
            .ThenBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task AddAsync(JiraClone.Domain.Entities.ActivityLog activityLog, CancellationToken cancellationToken = default) =>
        _dbContext.ActivityLogs.AddAsync(activityLog, cancellationToken).AsTask();

    public Task<bool> ExistsIssueEntryAsync(int issueId, string fieldName, string newValue, CancellationToken cancellationToken = default) =>
        _dbContext.ActivityLogs.AnyAsync(x => x.IssueId == issueId && x.FieldName == fieldName && x.NewValue == newValue, cancellationToken);
}
