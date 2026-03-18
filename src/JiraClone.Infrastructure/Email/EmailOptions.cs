namespace JiraClone.Infrastructure.Email;

public sealed class EmailOptions
{
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public string? Password { get; init; }

    public bool HasServerConfiguration =>
        !string.IsNullOrWhiteSpace(SmtpHost) &&
        SmtpPort > 0 &&
        !string.IsNullOrWhiteSpace(FromAddress);
}
