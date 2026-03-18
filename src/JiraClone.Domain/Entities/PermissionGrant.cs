using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class PermissionGrant
{
    public int PermissionSchemeId { get; set; }
    public PermissionScheme PermissionScheme { get; set; } = null!;
    public Permission Permission { get; set; }
    public ProjectRole ProjectRole { get; set; }
}
