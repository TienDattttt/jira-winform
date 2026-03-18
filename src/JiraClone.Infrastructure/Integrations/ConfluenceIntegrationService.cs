using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Integrations;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Infrastructure.Integrations;

public sealed class ConfluenceIntegrationService : IConfluenceIntegrationService
{
    private const string ConfluencePageFieldName = "Confluence Page";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IntegrationConfigStore _configStore;
    private readonly IIssueRepository _issues;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ConfluenceIntegrationService> _logger;

    public ConfluenceIntegrationService(
        IntegrationConfigStore configStore,
        IIssueRepository issues,
        IActivityLogRepository activityLogs,
        IUnitOfWork unitOfWork,
        IPermissionService permissionService,
        ICurrentUserContext currentUserContext,
        HttpClient httpClient,
        ILogger<ConfluenceIntegrationService>? logger = null)
    {
        _configStore = configStore;
        _issues = issues;
        _activityLogs = activityLogs;
        _unitOfWork = unitOfWork;
        _permissionService = permissionService;
        _currentUserContext = currentUserContext;
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<ConfluenceIntegrationService>.Instance;
    }

    public Task<ConfluenceProjectConfig?> GetConfigAsync(int projectId, CancellationToken cancellationToken = default)
    {
        return _configStore.GetAsync<ConfluenceProjectConfig>(projectId, IntegrationNames.Confluence, cancellationToken);
    }

    public async Task ConfigureAsync(int projectId, ConfluenceProjectConfig config, bool isEnabled = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        await EnsureManageProjectPermissionAsync(projectId, cancellationToken);

        var normalized = new ConfluenceProjectConfig(
            NormalizeBaseUrl(config.BaseUrl),
            NormalizeRequired(config.SpaceKey, "Confluence space key"),
            NormalizeRequired(config.ApiToken, "Confluence API token"),
            NormalizeRequired(config.Email, "Confluence email"));

        await _configStore.SaveAsync(projectId, IntegrationNames.Confluence, normalized, isEnabled, cancellationToken: cancellationToken);
        _logger.LogInformation("Configured Confluence integration for project {ProjectId} ({BaseUrl}/{SpaceKey}).", projectId, normalized.BaseUrl, normalized.SpaceKey);
    }

    public async Task DisconnectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        await EnsureManageProjectPermissionAsync(projectId, cancellationToken);
        await _configStore.RemoveAsync(projectId, IntegrationNames.Confluence, cancellationToken);
        _logger.LogInformation("Disconnected Confluence integration for project {ProjectId}.", projectId);
    }

    public async Task<IReadOnlyList<ConfluencePageLinkDto>> GetIssuePagesAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var activity = await _activityLogs.GetIssueActivityAsync(issueId, cancellationToken);
        return activity
            .Where(x => x.FieldName == ConfluencePageFieldName)
            .Select(x => Deserialize<ConfluencePageMetadata>(x.MetadataJson))
            .Where(x => x is not null)
            .Select(x => new ConfluencePageLinkDto(x!.Title, x.Url, x.LinkedAtUtc))
            .OrderByDescending(x => x.LinkedAtUtc)
            .ToList();
    }

    public async Task AddPageLinkAsync(int issueId, string title, string url, int userId, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken)
            ?? throw new ValidationException($"Issue {issueId} was not found.");
        await EnsureEditIssuePermissionAsync(issue.ProjectId, userId, cancellationToken);

        var normalizedUrl = NormalizeAbsoluteUrl(url, "Confluence page URL");
        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? normalizedUrl : title.Trim();
        if (await _activityLogs.ExistsIssueEntryAsync(issueId, ConfluencePageFieldName, normalizedUrl, cancellationToken))
        {
            return;
        }

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = issue.ProjectId,
            IssueId = issue.Id,
            UserId = userId,
            ActionType = ActivityActionType.Updated,
            FieldName = ConfluencePageFieldName,
            NewValue = normalizedUrl,
            OccurredAtUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new ConfluencePageMetadata(normalizedTitle, normalizedUrl, DateTime.UtcNow), JsonOptions)
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConfluencePageLinkDto> CreatePageFromIssueAsync(int issueId, int userId, CancellationToken cancellationToken = default)
    {
        var issue = await _issues.GetByIdAsync(issueId, cancellationToken)
            ?? throw new ValidationException($"Issue {issueId} was not found.");
        await EnsureEditIssuePermissionAsync(issue.ProjectId, userId, cancellationToken);

        var config = await GetConfigAsync(issue.ProjectId, cancellationToken)
            ?? throw new InvalidOperationException("Confluence is not configured for this project.");

        var requestUrl = BuildContentEndpoint(config.BaseUrl);
        var requestBody = new JsonObject
        {
            ["type"] = "page",
            ["title"] = issue.Title,
            ["space"] = new JsonObject { ["key"] = config.SpaceKey },
            ["body"] = new JsonObject
            {
                ["storage"] = new JsonObject
                {
                    ["value"] = string.IsNullOrWhiteSpace(issue.DescriptionHtml)
                        ? $"<p>{WebUtility.HtmlEncode(issue.Title)}</p>"
                        : issue.DescriptionHtml,
                    ["representation"] = "storage"
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Email}:{config.ApiToken}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Confluence page creation failed with status {(int)response.StatusCode}: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var pageTitle = root.TryGetProperty("title", out var titleProperty) ? titleProperty.GetString() ?? issue.Title : issue.Title;
        var pageUrl = ExtractPageUrl(root, config.BaseUrl);

        await AddPageLinkAsync(issueId, pageTitle, pageUrl, userId, cancellationToken);
        _logger.LogInformation("Created Confluence page for issue {IssueId} at {PageUrl}.", issueId, pageUrl);
        return new ConfluencePageLinkDto(pageTitle, pageUrl, DateTime.UtcNow);
    }

    private async Task EnsureManageProjectPermissionAsync(int projectId, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserContext.RequireUserId();
        if (!await _permissionService.HasPermissionAsync(currentUserId, projectId, Permission.ManageProject, cancellationToken))
        {
            throw new UnauthorizedAccessException("Current user does not have permission to manage project integrations.");
        }
    }

    private async Task EnsureEditIssuePermissionAsync(int projectId, int userId, CancellationToken cancellationToken)
    {
        if (!await _permissionService.HasPermissionAsync(userId, projectId, Permission.EditIssue, cancellationToken))
        {
            throw new UnauthorizedAccessException("Current user does not have permission to edit this issue.");
        }
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string NormalizeBaseUrl(string? value)
    {
        var url = NormalizeAbsoluteUrl(value, "Confluence base URL");
        return url.TrimEnd('/');
    }

    private static string NormalizeAbsoluteUrl(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ValidationException($"{fieldName} must be a valid http or https URL.");
        }

        return uri.ToString();
    }

    private static string BuildContentEndpoint(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        return normalized.Contains("/wiki", StringComparison.OrdinalIgnoreCase)
            ? $"{normalized}/rest/api/content"
            : $"{normalized}/wiki/rest/api/content";
    }

    private static string ExtractPageUrl(JsonElement root, string baseUrl)
    {
        if (root.TryGetProperty("_links", out var links))
        {
            var webUi = links.TryGetProperty("webui", out var webUiProperty) ? webUiProperty.GetString() : null;
            if (!string.IsNullOrWhiteSpace(webUi))
            {
                return CombineUrl(baseUrl, webUi);
            }
        }

        if (root.TryGetProperty("id", out var idProperty) && !string.IsNullOrWhiteSpace(idProperty.GetString()))
        {
            return CombineUrl(baseUrl, $"/wiki/pages/viewpage.action?pageId={idProperty.GetString()}");
        }

        return baseUrl;
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        return new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path.TrimStart('/')).ToString();
    }

    private static TMetadata? Deserialize<TMetadata>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<TMetadata>(json, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private sealed record ConfluencePageMetadata(string Title, string Url, DateTime LinkedAtUtc);
}
