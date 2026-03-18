using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class ProjectIntegrationConfig : AggregateRoot
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string IntegrationName { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastSyncAtUtc { get; set; }
}
