using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class ActivityLogRepository : IActivityLogRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public ActivityLogRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ActivityLog>> GetIssueActivityAsync(int issueId, CancellationToken cancellationToken = default) =>
        await _dbContext.ActivityLogs
            .Include(x => x.User)
            .Where(x => x.IssueId == issueId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ToListAsync(cancellationToken);

    public Task AddAsync(ActivityLog activityLog, CancellationToken cancellationToken = default) =>
        _dbContext.ActivityLogs.AddAsync(activityLog, cancellationToken).AsTask();
}
