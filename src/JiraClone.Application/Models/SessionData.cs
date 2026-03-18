namespace JiraClone.Application.Models;

public sealed class SessionData
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
}
