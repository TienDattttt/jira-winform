using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;

namespace JiraClone.Application.Projects;

public class ProjectQueryService
{
    private readonly IProjectRepository _projects;

    public ProjectQueryService(IProjectRepository projects)
    {
        _projects = projects;
    }

    public Task<Project?> GetActiveProjectAsync(CancellationToken cancellationToken = default) =>
        _projects.GetActiveProjectAsync(cancellationToken);

    public Task<IReadOnlyList<Project>> GetAccessibleProjectsAsync(int userId, CancellationToken cancellationToken = default) =>
        _projects.GetAccessibleProjectsAsync(userId, cancellationToken);

    public Task<Project?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default) =>
        _projects.GetByIdAsync(projectId, cancellationToken);
}
