using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class Attachment : AuditableEntity
{
    public int IssueId { get; set; }
    public Issue Issue { get; set; } = null!;
    public string StoredFileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public int UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
}
