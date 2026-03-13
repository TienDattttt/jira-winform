namespace JiraClone.Persistence.Schema.Entities;

public class RoleEntity : SchemaEntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<UserRoleEntity> UserRoles { get; set; } = new List<UserRoleEntity>();
}

public class UserEntity : SchemaEntityBase
{
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<UserRoleEntity> UserRoles { get; set; } = new List<UserRoleEntity>();
    public ICollection<ProjectMemberEntity> ProjectMembers { get; set; } = new List<ProjectMemberEntity>();
    public ICollection<IssueAssigneeEntity> IssueAssignees { get; set; } = new List<IssueAssigneeEntity>();
}

public class UserRoleEntity : SchemaEntityBase
{
    public int UserId { get; set; }
    public UserEntity User { get; set; } = null!;
    public int RoleId { get; set; }
    public RoleEntity Role { get; set; } = null!;
}

public class ProjectEntity : SchemaEntityBase
{
    public string ProjectKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ProjectMemberEntity> Members { get; set; } = new List<ProjectMemberEntity>();
    public ICollection<BoardColumnEntity> BoardColumns { get; set; } = new List<BoardColumnEntity>();
    public ICollection<SprintEntity> Sprints { get; set; } = new List<SprintEntity>();
    public ICollection<IssueEntity> Issues { get; set; } = new List<IssueEntity>();
}

public class ProjectMemberEntity : SchemaEntityBase
{
    public int ProjectId { get; set; }
    public ProjectEntity Project { get; set; } = null!;
    public int UserId { get; set; }
    public UserEntity User { get; set; } = null!;
    public string ProjectRole { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class IssueTypeEntity : SchemaEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public class IssueStatusEntity : SchemaEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string StatusCategory { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class PriorityEntity : SchemaEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Weight { get; set; }
    public string? ColorHex { get; set; }
    public int SortOrder { get; set; }
}

public class BoardColumnEntity : SchemaEntityBase
{
    public int ProjectId { get; set; }
    public ProjectEntity Project { get; set; } = null!;
    public int IssueStatusId { get; set; }
    public IssueStatusEntity IssueStatus { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public int? WipLimit { get; set; }
}

public class SprintEntity : SchemaEntityBase
{
    public int ProjectId { get; set; }
    public ProjectEntity Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string SprintState { get; set; } = string.Empty;
    public DateTime? ClosedAt { get; set; }
    public ICollection<IssueEntity> Issues { get; set; } = new List<IssueEntity>();
}

public class IssueEntity : SchemaEntityBase
{
    public int ProjectId { get; set; }
    public ProjectEntity Project { get; set; } = null!;
    public int? SprintId { get; set; }
    public SprintEntity? Sprint { get; set; }
    public string IssueKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? DescriptionHtml { get; set; }
    public string? DescriptionText { get; set; }
    public int IssueTypeId { get; set; }
    public IssueTypeEntity IssueType { get; set; } = null!;
    public int IssueStatusId { get; set; }
    public IssueStatusEntity IssueStatus { get; set; } = null!;
    public int PriorityId { get; set; }
    public PriorityEntity Priority { get; set; } = null!;
    public int ReporterId { get; set; }
    public UserEntity Reporter { get; set; } = null!;
    public int CreatedById { get; set; }
    public UserEntity CreatedBy { get; set; } = null!;
    public int? EstimateHours { get; set; }
    public int? TimeSpentHours { get; set; }
    public int? TimeRemainingHours { get; set; }
    public int? StoryPoints { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal BoardPosition { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public ICollection<IssueAssigneeEntity> Assignees { get; set; } = new List<IssueAssigneeEntity>();
    public ICollection<CommentEntity> Comments { get; set; } = new List<CommentEntity>();
    public ICollection<AttachmentEntity> Attachments { get; set; } = new List<AttachmentEntity>();
    public ICollection<ActivityLogEntity> ActivityLogs { get; set; } = new List<ActivityLogEntity>();
}

public class IssueAssigneeEntity : SchemaEntityBase
{
    public int IssueId { get; set; }
    public IssueEntity Issue { get; set; } = null!;
    public int UserId { get; set; }
    public UserEntity User { get; set; } = null!;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

public class CommentEntity : SchemaEntityBase
{
    public int IssueId { get; set; }
    public IssueEntity Issue { get; set; } = null!;
    public int UserId { get; set; }
    public UserEntity User { get; set; } = null!;
    public string Body { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class AttachmentEntity : SchemaEntityBase
{
    public int IssueId { get; set; }
    public IssueEntity Issue { get; set; } = null!;
    public string StoredFileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public int UploadedById { get; set; }
    public UserEntity UploadedBy { get; set; } = null!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string ChecksumSha256 { get; set; } = string.Empty;
}

public class ActivityLogEntity : SchemaEntityBase
{
    public int ProjectId { get; set; }
    public ProjectEntity Project { get; set; } = null!;
    public int? IssueId { get; set; }
    public IssueEntity? Issue { get; set; }
    public int UserId { get; set; }
    public UserEntity User { get; set; } = null!;
    public string ActionType { get; set; } = string.Empty;
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? MetadataJson { get; set; }
}
