using JiraClone.Application.Models;
using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface ISprintService
{
    Task<IReadOnlyList<Sprint>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<Sprint?> GetActiveByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Issue>> GetAssignableIssuesAsync(int projectId, CancellationToken cancellationToken = default);
    Task<Sprint> CreateAsync(int projectId, string name, string? goal, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default);
    Task<bool> AssignIssuesAsync(int sprintId, IReadOnlyCollection<int> issueIds, CancellationToken cancellationToken = default);
    Task<bool> StartSprintAsync(int sprintId, CancellationToken cancellationToken = default);
    Task<bool> CloseSprintAsync(int sprintId, int? moveToSprintId, CancellationToken cancellationToken = default);
    Task<BurndownReportDto?> GetBurndownDataAsync(int sprintId, CancellationToken cancellationToken = default);
    Task<VelocityReportDto> GetVelocityDataAsync(int projectId, int lastN = 6, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CfdDataPointDto>> GetCfdDataAsync(int projectId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<SprintReportDto?> GetSprintReportAsync(int sprintId, CancellationToken cancellationToken = default);
}
