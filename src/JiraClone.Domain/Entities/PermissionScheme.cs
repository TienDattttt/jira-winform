using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class PermissionScheme : AggregateRoot
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public ICollection<PermissionGrant> Grants { get; set; } = new List<PermissionGrant>();
}
