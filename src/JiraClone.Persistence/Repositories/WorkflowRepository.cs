using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class WorkflowRepository : IWorkflowRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public WorkflowRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<WorkflowDefinition?> GetDefaultByProjectAsync(int projectId, CancellationToken cancellationToken = default) =>
        _dbContext.WorkflowDefinitions
            .Include(x => x.Statuses)
            .Include(x => x.Transitions)
            .ThenInclude(x => x.FromStatus)
            .Include(x => x.Transitions)
            .ThenInclude(x => x.ToStatus)
            .Include(x => x.Transitions)
            .ThenInclude(x => x.AllowedRoles)
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.IsDefault, cancellationToken);

    public Task<WorkflowDefinition?> GetByIdAsync(int workflowDefinitionId, CancellationToken cancellationToken = default) =>
        _dbContext.WorkflowDefinitions
            .Include(x => x.Statuses)
            .Include(x => x.Transitions)
            .ThenInclude(x => x.FromStatus)
            .Include(x => x.Transitions)
            .ThenInclude(x => x.ToStatus)
            .Include(x => x.Transitions)
            .ThenInclude(x => x.AllowedRoles)
            .FirstOrDefaultAsync(x => x.Id == workflowDefinitionId, cancellationToken);

    public Task<WorkflowStatus?> GetStatusByIdAsync(int workflowStatusId, CancellationToken cancellationToken = default) =>
        _dbContext.WorkflowStatuses
            .Include(x => x.WorkflowDefinition)
            .ThenInclude(x => x.Project)
            .Include(x => x.WorkflowDefinition)
            .ThenInclude(x => x.Statuses)
            .Include(x => x.OutgoingTransitions)
            .ThenInclude(x => x.AllowedRoles)
            .Include(x => x.OutgoingTransitions)
            .ThenInclude(x => x.ToStatus)
            .Include(x => x.IncomingTransitions)
            .FirstOrDefaultAsync(x => x.Id == workflowStatusId, cancellationToken);

    public Task<WorkflowTransition?> GetTransitionAsync(int workflowDefinitionId, int fromStatusId, int toStatusId, CancellationToken cancellationToken = default) =>
        _dbContext.WorkflowTransitions
            .Include(x => x.AllowedRoles)
            .Include(x => x.FromStatus)
            .Include(x => x.ToStatus)
            .FirstOrDefaultAsync(
                x => x.WorkflowDefinitionId == workflowDefinitionId && x.FromStatusId == fromStatusId && x.ToStatusId == toStatusId,
                cancellationToken);

    public async Task<IReadOnlyList<WorkflowTransition>> GetTransitionsFromStatusAsync(int workflowDefinitionId, int fromStatusId, CancellationToken cancellationToken = default) =>
        await _dbContext.WorkflowTransitions
            .Include(x => x.AllowedRoles)
            .Include(x => x.FromStatus)
            .Include(x => x.ToStatus)
            .Where(x => x.WorkflowDefinitionId == workflowDefinitionId && x.FromStatusId == fromStatusId)
            .OrderBy(x => x.ToStatus.DisplayOrder)
            .ToListAsync(cancellationToken);

    public Task AddAsync(WorkflowDefinition workflowDefinition, CancellationToken cancellationToken = default) =>
        _dbContext.WorkflowDefinitions.AddAsync(workflowDefinition, cancellationToken).AsTask();
}
