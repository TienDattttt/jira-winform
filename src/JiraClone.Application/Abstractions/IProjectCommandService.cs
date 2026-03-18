using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Abstractions;

public interface IProjectCommandService
{
    Task<Project> CreateProjectAsync(string name, string key, ProjectCategory category, string? description, IReadOnlyCollection<ProjectMemberInput> members, CancellationToken cancellationToken = default);
    Task<bool> ProjectKeyExistsAsync(string key, int? excludeProjectId = null, CancellationToken cancellationToken = default);
    Task<Project?> UpdateProjectAsync(int projectId, string name, string? description, ProjectCategory category, BoardType boardType, string? url, CancellationToken cancellationToken = default);
    Task<bool> ArchiveProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<bool> DeleteProjectAsync(int projectId, int userId, CancellationToken cancellationToken = default);
    Task<bool> AddMemberAsync(int projectId, int userId, ProjectRole projectRole, CancellationToken cancellationToken = default);
    Task<bool> UpdateMemberRoleAsync(int projectId, int userId, ProjectRole projectRole, CancellationToken cancellationToken = default);
    Task<bool> RemoveMemberAsync(int projectId, int userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateBoardColumnAsync(int projectId, int boardColumnId, string name, int? wipLimit, CancellationToken cancellationToken = default);
    Task<bool> UpdatePermissionSchemeAsync(int projectId, string name, IReadOnlyCollection<PermissionGrantInput> grants, CancellationToken cancellationToken = default);
}



