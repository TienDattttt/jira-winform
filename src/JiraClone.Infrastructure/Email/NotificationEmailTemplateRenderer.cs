using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Infrastructure.Email;

public sealed class NotificationEmailTemplateRenderer : INotificationEmailTemplateRenderer
{
    private static readonly Regex PlaceholderRegex = new("{{([A-Za-z0-9]+)}}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly Assembly _assembly = typeof(NotificationEmailTemplateRenderer).Assembly;
    private readonly ILogger<NotificationEmailTemplateRenderer> _logger;

    public NotificationEmailTemplateRenderer(ILogger<NotificationEmailTemplateRenderer>? logger = null)
    {
        _logger = logger ?? NullLogger<NotificationEmailTemplateRenderer>.Instance;
    }

    public string Render(NotificationEmailTemplateModel model)
    {
        var templateName = ResolveTemplateName(model.Type);
        var template = LoadTemplate(templateName) ?? BuildFallbackTemplate();
        var values = BuildValues(model);

        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return values.TryGetValue(key, out var value) ? value : string.Empty;
        });
    }

    private string? LoadTemplate(string templateFileName)
    {
        var resourceName = _assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(templateFileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            _logger.LogWarning("Notification email template {TemplateFileName} was not found.", templateFileName);
            return null;
        }

        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            _logger.LogWarning("Notification email template resource {TemplateFileName} could not be loaded.", templateFileName);
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static Dictionary<string, string> BuildValues(NotificationEmailTemplateModel model)
    {
        var recipientName = Encode(model.RecipientName);
        var title = Encode(model.Title);
        var body = EncodeMultiline(model.Body);
        var issueKey = Encode(model.IssueKey ?? "Issue");
        var issueTitle = Encode(model.IssueTitle ?? model.Title);
        var projectName = Encode(model.ProjectName ?? "your project");
        var sprintName = Encode(model.SprintName ?? model.Title);

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RecipientName"] = string.IsNullOrWhiteSpace(recipientName) ? "there" : recipientName,
            ["Title"] = title,
            ["Body"] = body,
            ["IssueKey"] = issueKey,
            ["IssueTitle"] = issueTitle,
            ["ProjectName"] = projectName,
            ["SprintName"] = sprintName,
            ["SummaryLine"] = BuildSummaryLine(model, issueKey, projectName, sprintName, body),
        };
    }

    private static string BuildSummaryLine(NotificationEmailTemplateModel model, string issueKey, string projectName, string sprintName, string body) => model.Type switch
    {
        NotificationType.IssueAssigned => $"You've been assigned <strong>{issueKey}</strong> in project <strong>{projectName}</strong>.",
        NotificationType.CommentAdded or NotificationType.CommentMentioned => body,
        NotificationType.SprintStarted => $"Sprint <strong>{sprintName}</strong> has started in <strong>{projectName}</strong>.",
        NotificationType.SprintCompleted => $"Sprint <strong>{sprintName}</strong> has completed in <strong>{projectName}</strong>.",
        _ => body,
    };

    private static string ResolveTemplateName(NotificationType type) => type switch
    {
        NotificationType.IssueAssigned => "IssueAssigned.html",
        NotificationType.CommentAdded or NotificationType.CommentMentioned => "CommentAdded.html",
        NotificationType.SprintStarted => "SprintStarted.html",
        NotificationType.SprintCompleted => "SprintCompleted.html",
        _ => "GenericNotification.html",
    };

    private static string Encode(string value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

    private static string EncodeMultiline(string value) => Encode(value).Replace("\r\n", "<br />").Replace("\n", "<br />");

    private static string BuildFallbackTemplate() => @"<!DOCTYPE html>
<html>
<head>
  <meta charset=""utf-8"" />
  <title>{{Title}}</title>
</head>
<body style=""font-family:Segoe UI,Arial,sans-serif;background:#F4F5F7;margin:0;padding:24px;color:#172B4D;"">
  <div style=""max-width:640px;margin:0 auto;background:#FFFFFF;border:1px solid #DFE1E6;border-radius:12px;padding:24px;"">
    <h2 style=""margin:0 0 12px 0;font-size:22px;"">{{Title}}</h2>
    <p style=""margin:0 0 12px 0;font-size:14px;color:#42526E;"">Hello {{RecipientName}},</p>
    <p style=""margin:0;font-size:14px;line-height:1.6;"">{{Body}}</p>
  </div>
</body>
</html>";
}
