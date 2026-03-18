using JiraClone.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;

namespace JiraClone.Infrastructure.Email;

public sealed class MailKitEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<MailKitEmailService> _logger;

    public MailKitEmailService(EmailOptions options, ILogger<MailKitEmailService>? logger = null)
    {
        _options = options;
        _logger = logger ?? NullLogger<MailKitEmailService>.Instance;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!_options.HasServerConfiguration)
        {
            _logger.LogWarning("Email send skipped because SMTP configuration is incomplete.");
            return;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("Email send skipped because recipient address is empty.");
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(string.IsNullOrWhiteSpace(toName) ? toEmail : toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = ResolveSocketOptions(_options);
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, socketOptions, ct);

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
        _logger.LogInformation("Sent notification email to {RecipientEmail} with subject {Subject}.", toEmail, subject);
    }

    private static SecureSocketOptions ResolveSocketOptions(EmailOptions options)
    {
        if (!options.UseSsl)
        {
            return SecureSocketOptions.None;
        }

        return options.SmtpPort == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
    }
}
