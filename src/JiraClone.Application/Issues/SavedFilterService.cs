using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Issues;

public class SavedFilterService : ISavedFilterService
{
    private readonly ISavedFilterRepository _savedFilters;
    private readonly IProjectRepository _projects;
    private readonly IAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SavedFilterService> _logger;

    public SavedFilterService(ISavedFilterRepository savedFilters, IProjectRepository projects, IAuthorizationService authorization, IUnitOfWork unitOfWork, ILogger<SavedFilterService>? logger = null)
    {
        _savedFilters = savedFilters;
        _projects = projects;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<SavedFilterService>.Instance;
    }

    public async Task<IReadOnlyList<SavedFilterDto>> GetByProjectAsync(int projectId, int userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading saved filters for project {ProjectId} and user {UserId}.", projectId, userId);
        await EnsureProjectAccessAsync(projectId, userId, cancellationToken);
        return (await _savedFilters.GetByProjectAsync(projectId, userId, cancellationToken)).Select(Map).ToList();
    }

    public async Task<SavedFilterDto> CreateAsync(int projectId, int userId, string name, string queryText, bool isFavorite = false, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating saved filter in project {ProjectId} for user {UserId}.", projectId, userId);
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager, RoleCatalog.Developer, RoleCatalog.Viewer);
        await EnsureProjectAccessAsync(projectId, userId, cancellationToken);
        var savedFilter = new SavedFilter
        {
            ProjectId = projectId,
            UserId = userId,
            Name = NormalizeName(name),
            QueryText = NormalizeQuery(queryText),
            IsFavorite = isFavorite
        };

        await _savedFilters.AddAsync(savedFilter, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(savedFilter);
    }

    public async Task<SavedFilterDto?> UpdateAsync(int savedFilterId, int userId, string name, string queryText, bool isFavorite = false, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating saved filter {SavedFilterId} for user {UserId}.", savedFilterId, userId);
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager, RoleCatalog.Developer, RoleCatalog.Viewer);
        var savedFilter = await _savedFilters.GetByIdAsync(savedFilterId, cancellationToken);
        if (savedFilter is null || savedFilter.UserId != userId)
        {
            _logger.LogWarning("Saved filter {SavedFilterId} could not be updated because it was not found or did not belong to user {UserId}.", savedFilterId, userId);
            return null;
        }

        await EnsureProjectAccessAsync(savedFilter.ProjectId, userId, cancellationToken);
        savedFilter.Name = NormalizeName(name);
        savedFilter.QueryText = NormalizeQuery(queryText);
        savedFilter.IsFavorite = isFavorite;
        savedFilter.UpdatedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(savedFilter);
    }

    public async Task<bool> DeleteAsync(int savedFilterId, int userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting saved filter {SavedFilterId} for user {UserId}.", savedFilterId, userId);
        _authorization.EnsureInRole(RoleCatalog.Admin, RoleCatalog.ProjectManager, RoleCatalog.Developer, RoleCatalog.Viewer);
        var savedFilter = await _savedFilters.GetByIdAsync(savedFilterId, cancellationToken);
        if (savedFilter is null || savedFilter.UserId != userId)
        {
            _logger.LogWarning("Saved filter {SavedFilterId} could not be deleted because it was not found or did not belong to user {UserId}.", savedFilterId, userId);
            return false;
        }

        await _savedFilters.RemoveAsync(savedFilter, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task EnsureProjectAccessAsync(int projectId, int userId, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(projectId, cancellationToken)
            ?? throw new InvalidOperationException("Project was not found.");
        if (project.Members.All(x => x.UserId != userId))
        {
            throw new InvalidOperationException("The user does not have access to this project.");
        }
    }

    private static SavedFilterDto Map(SavedFilter savedFilter) => new(savedFilter.Id, savedFilter.ProjectId, savedFilter.UserId, savedFilter.Name, savedFilter.QueryText, savedFilter.IsFavorite);

    private static string NormalizeName(string name) => string.IsNullOrWhiteSpace(name) ? throw new InvalidOperationException("Filter name is required.") : name.Trim();

    private static string NormalizeQuery(string queryText) => string.IsNullOrWhiteSpace(queryText) ? throw new InvalidOperationException("JQL query is required.") : queryText.Trim();
}
