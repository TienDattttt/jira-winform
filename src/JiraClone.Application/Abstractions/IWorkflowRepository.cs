using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IWorkflowRepository
{
    Task<WorkflowDefinition?> GetDefaultByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<WorkflowDefinition?> GetByIdAsync(int workflowDefinitionId, CancellationToken cancellationToken = default);
    Task<WorkflowStatus?> GetStatusByIdAsync(int workflowStatusId, CancellationToken cancellationToken = default);
    Task<WorkflowTransition?> GetTransitionAsync(int workflowDefinitionId, int fromStatusId, int toStatusId, CancellationToken cancellationToken = default);
    Task<WorkflowTransition?> GetTransitionByIdAsync(int transitionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTransition>> GetTransitionsFromStatusAsync(int workflowDefinitionId, int fromStatusId, CancellationToken cancellationToken = default);
    Task AddAsync(WorkflowDefinition workflowDefinition, CancellationToken cancellationToken = default);
}
