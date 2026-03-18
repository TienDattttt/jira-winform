using JiraClone.Domain.Common;
using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class Project : AggregateRoot
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectCategory Category { get; set; } = ProjectCategory.Software;
    public BoardType BoardType { get; set; } = BoardType.Scrum;
    public string? Url { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<BoardColumn> BoardColumns { get; set; } = new List<BoardColumn>();
    public ICollection<WorkflowDefinition> WorkflowDefinitions { get; set; } = new List<WorkflowDefinition>();
    public PermissionScheme? PermissionScheme { get; set; }
    public ICollection<Label> Labels { get; set; } = new List<Label>();
    public ICollection<Component> Components { get; set; } = new List<Component>();
    public ICollection<ProjectVersion> Versions { get; set; } = new List<ProjectVersion>();
    public ICollection<SavedFilter> SavedFilters { get; set; } = new List<SavedFilter>();
    public ICollection<Issue> Issues { get; set; } = new List<Issue>();
    public ICollection<Sprint> Sprints { get; set; } = new List<Sprint>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}


