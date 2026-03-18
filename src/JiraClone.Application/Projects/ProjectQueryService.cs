using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Projects;

public class ProjectQueryService
{
    private readonly IProjectRepository _projects;
    private readonly ILogger<ProjectQueryService> _logger;

    public ProjectQueryService(IProjectRepository projects, ILogger<ProjectQueryService>? logger = null)
    {
        _projects = projects;
        _logger = logger ?? NullLogger<ProjectQueryService>.Instance;
    }

    public Task<Project?> GetActiveProjectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading active project.");
        return _projects.GetActiveProjectAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Project>> GetAccessibleProjectsAsync(int userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading accessible projects for user {UserId}.", userId);
        return _projects.GetAccessibleProjectsAsync(userId, cancellationToken);
    }

    public Task<Project?> GetByIdAsync(int projectId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading project {ProjectId}.", projectId);
        return _projects.GetByIdAsync(projectId, cancellationToken);
    }
}
