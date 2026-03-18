using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Permissions;

public sealed record PermissionGrantTemplate(Permission Permission, ProjectRole ProjectRole);

public static class PermissionDefaults
{
    public const string DefaultSchemeName = "Default Permission Scheme";

    private static readonly IReadOnlyList<PermissionGrantTemplate> DefaultGrants =
    [
        new(Permission.ViewProject, ProjectRole.Viewer),

        new(Permission.ViewProject, ProjectRole.Developer),
        new(Permission.CreateIssue, ProjectRole.Developer),
        new(Permission.EditIssue, ProjectRole.Developer),
        new(Permission.DeleteIssue, ProjectRole.Developer),
        new(Permission.TransitionIssue, ProjectRole.Developer),
        new(Permission.AddComment, ProjectRole.Developer),
        new(Permission.EditOwnComment, ProjectRole.Developer),
        new(Permission.DeleteOwnComment, ProjectRole.Developer),

        new(Permission.ViewProject, ProjectRole.ProjectManager),
        new(Permission.CreateIssue, ProjectRole.ProjectManager),
        new(Permission.EditIssue, ProjectRole.ProjectManager),
        new(Permission.DeleteIssue, ProjectRole.ProjectManager),
        new(Permission.TransitionIssue, ProjectRole.ProjectManager),
        new(Permission.ManageSprints, ProjectRole.ProjectManager),
        new(Permission.ManageBoard, ProjectRole.ProjectManager),
        new(Permission.ManageProject, ProjectRole.ProjectManager),
        new(Permission.ManageMembers, ProjectRole.ProjectManager),
        new(Permission.AddComment, ProjectRole.ProjectManager),
        new(Permission.EditOwnComment, ProjectRole.ProjectManager),
        new(Permission.DeleteOwnComment, ProjectRole.ProjectManager),

        new(Permission.ViewProject, ProjectRole.Admin),
        new(Permission.CreateIssue, ProjectRole.Admin),
        new(Permission.EditIssue, ProjectRole.Admin),
        new(Permission.DeleteIssue, ProjectRole.Admin),
        new(Permission.TransitionIssue, ProjectRole.Admin),
        new(Permission.ManageSprints, ProjectRole.Admin),
        new(Permission.ManageBoard, ProjectRole.Admin),
        new(Permission.ManageProject, ProjectRole.Admin),
        new(Permission.ManageMembers, ProjectRole.Admin),
        new(Permission.AddComment, ProjectRole.Admin),
        new(Permission.EditOwnComment, ProjectRole.Admin),
        new(Permission.DeleteOwnComment, ProjectRole.Admin),
    ];

    public static IReadOnlyList<PermissionGrantTemplate> GetDefaultGrants() => DefaultGrants;

    public static IReadOnlyList<Permission> GetPermissionsForRole(ProjectRole projectRole) =>
        DefaultGrants
            .Where(x => x.ProjectRole == projectRole)
            .Select(x => x.Permission)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
}
