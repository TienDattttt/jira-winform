using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class IssueRepository : IIssueRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public IssueRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IReadOnlyList<Issue>> GetBoardIssuesAsync(int projectId, CancellationToken cancellationToken = default) =>
        GetBoardIssuesAsync(projectId, sprintId: null, cancellationToken);

    public async Task<IReadOnlyList<Issue>> GetBoardIssuesAsync(int projectId, int? sprintId, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Issues
            .Include(x => x.Reporter)
            .Include(x => x.Assignees)
            .ThenInclude(x => x.User)
            .Include(x => x.ParentIssue)
            .Include(x => x.WorkflowStatus)
            .Where(x => x.ProjectId == projectId && !x.IsDeleted);

        if (sprintId.HasValue)
        {
            query = query.Where(x => x.SprintId == sprintId.Value);
        }

        return await query
            .OrderBy(x => x.WorkflowStatus.DisplayOrder)
            .ThenBy(x => x.BoardPosition)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Issue>> GetProjectIssuesAsync(int projectId, CancellationToken cancellationToken = default) =>
        await BuildNavigatorQuery()
            .Where(x => x.ProjectId == projectId && !x.IsDeleted)
            .OrderBy(x => x.WorkflowStatus.DisplayOrder)
            .ThenBy(x => x.Title)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Issue>> ExecuteQueryAsync(int projectId, Func<IQueryable<Issue>, IQueryable<Issue>> queryShaper, CancellationToken cancellationToken = default)
    {
        var query = BuildNavigatorQuery().Where(x => x.ProjectId == projectId && !x.IsDeleted);
        query = (queryShaper ?? (source => source))(query);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Issue>> GetIncompleteBySprintIdAsync(int sprintId, CancellationToken cancellationToken = default) =>
        await _dbContext.Issues
            .Include(x => x.Reporter)
            .Include(x => x.WorkflowStatus)
            .Where(x => x.SprintId == sprintId && x.WorkflowStatus.Category != StatusCategory.Done && !x.IsDeleted)
            .OrderBy(x => x.WorkflowStatus.DisplayOrder)
            .ThenBy(x => x.BoardPosition)
            .ToListAsync(cancellationToken);

    public Task<Issue?> GetByIdAsync(int issueId, CancellationToken cancellationToken = default) =>
        _dbContext.Issues
            .Include(x => x.Reporter)
            .Include(x => x.Assignees)
            .ThenInclude(x => x.User)
            .Include(x => x.Attachments)
            .Include(x => x.ParentIssue)
            .Include(x => x.FixVersion)
            .Include(x => x.WorkflowStatus)
            .ThenInclude(x => x.WorkflowDefinition)
            .ThenInclude(x => x.Statuses)
            .Include(x => x.IssueLabels)
            .ThenInclude(x => x.Label)
            .Include(x => x.IssueComponents)
            .ThenInclude(x => x.Component)
            .FirstOrDefaultAsync(x => x.Id == issueId && !x.IsDeleted, cancellationToken);

    public async Task<decimal> GetNextBoardPositionAsync(int projectId, int workflowStatusId, CancellationToken cancellationToken = default)
    {
        var first = await _dbContext.Issues
            .Where(x => x.ProjectId == projectId && x.WorkflowStatusId == workflowStatusId && !x.IsDeleted)
            .OrderBy(x => x.BoardPosition)
            .Select(x => (decimal?)x.BoardPosition)
            .FirstOrDefaultAsync(cancellationToken);

        return first is null ? 1m : first.Value - 1m;
    }

    public Task AddAsync(Issue issue, CancellationToken cancellationToken = default) =>
        _dbContext.Issues.AddAsync(issue, cancellationToken).AsTask();

    public Task RemoveAsync(Issue issue, CancellationToken cancellationToken = default)
    {
        issue.IsDeleted = true;
        issue.UpdatedAtUtc = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public async Task<int> GetNextIssueSequenceAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var prefix = await _dbContext.Projects
            .Where(p => p.Id == projectId)
            .Select(p => p.Key.Trim().ToUpper() + "-")
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        if (string.IsNullOrEmpty(prefix))
        {
            return 1;
        }

        var maxSequence = await _dbContext.Database
            .SqlQueryRaw<int?>(
                @"SELECT MAX(
                    CASE
                        WHEN i.IssueKey LIKE @p0 + '%'
                        THEN TRY_CAST(SUBSTRING(i.IssueKey, LEN(@p0) + 1, LEN(i.IssueKey)) AS INT)
                        ELSE NULL
                    END
                ) AS [Value]
                FROM Issues i WITH (UPDLOCK, HOLDLOCK)
                WHERE i.ProjectId = @p1",
                prefix, projectId)
            .FirstOrDefaultAsync(cancellationToken);

        return (maxSequence ?? 0) + 1;
    }

    public async Task<IReadOnlyList<Issue>> GetSubIssuesAsync(int parentIssueId, CancellationToken cancellationToken = default) =>
        await _dbContext.Issues
            .Include(x => x.Reporter)
            .Include(x => x.Assignees)
            .ThenInclude(x => x.User)
            .Include(x => x.WorkflowStatus)
            .Where(x => x.ParentIssueId == parentIssueId && !x.IsDeleted)
            .OrderBy(x => x.WorkflowStatus.DisplayOrder)
            .ThenBy(x => x.Title)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Issue>> GetPotentialParentsAsync(int projectId, IssueType childType, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Issues
            .Where(x => x.ProjectId == projectId && !x.IsDeleted && x.Type != IssueType.Subtask);

        if (childType == IssueType.Subtask)
        {
            query = query.Where(x => x.Type != IssueType.Subtask);
        }
        else
        {
            query = query.Where(x => x.Type == IssueType.Epic);
        }

        return await query
            .Include(x => x.WorkflowStatus)
            .OrderBy(x => x.IssueKey)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Issue> BuildNavigatorQuery()
    {
        return _dbContext.Issues
            .AsSplitQuery()
            .Include(x => x.Project)
            .Include(x => x.Reporter)
            .Include(x => x.Sprint)
            .Include(x => x.WorkflowStatus)
            .Include(x => x.Assignees)
            .ThenInclude(x => x.User)
            .Include(x => x.IssueLabels)
            .ThenInclude(x => x.Label)
            .Include(x => x.IssueComponents)
            .ThenInclude(x => x.Component);
    }
}

