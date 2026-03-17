using JiraClone.Application.Models;
using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IWorkflowService
{
    Task<WorkflowDefinitionDto?> GetDefaultWorkflowAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowStatusOptionDto>> GetAllowedTransitionsAsync(int issueId, int userId, CancellationToken cancellationToken = default);
    Task<WorkflowTransitionResult> ExecuteTransitionAsync(int issueId, int toStatusId, int userId, decimal? boardPosition = null, CancellationToken cancellationToken = default);
    Task<WorkflowStatus> CreateStatusAsync(int projectId, string name, string color, JiraClone.Domain.Enums.StatusCategory category, CancellationToken cancellationToken = default);
    Task<WorkflowStatus?> UpdateStatusAsync(int workflowStatusId, string name, string color, JiraClone.Domain.Enums.StatusCategory category, CancellationToken cancellationToken = default);
    Task<bool> DeleteStatusAsync(int workflowStatusId, CancellationToken cancellationToken = default);
    Task<WorkflowTransition?> CreateTransitionAsync(int projectId, int fromStatusId, int toStatusId, string name, IReadOnlyCollection<string> allowedRoleNames, CancellationToken cancellationToken = default);
    Task<WorkflowTransition?> UpdateTransitionAsync(int transitionId, string name, IReadOnlyCollection<string> allowedRoleNames, CancellationToken cancellationToken = default);
    Task<bool> DeleteTransitionAsync(int transitionId, CancellationToken cancellationToken = default);
}
