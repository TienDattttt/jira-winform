namespace JiraClone.Domain.Entities;

public class IssueComponent
{
    public int IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public int ComponentId { get; set; }
    public Component Component { get; set; } = null!;
}
