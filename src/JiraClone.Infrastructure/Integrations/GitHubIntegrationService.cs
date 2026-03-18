using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Integrations;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Octokit;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;

namespace JiraClone.Infrastructure.Integrations;

public sealed class GitHubIntegrationService : IGitHubIntegrationService
{
    private const string CommitFieldName = "GitHub Commit";
    private const string PullRequestFieldName = "GitHub Pull Request";
    private static readonly Regex IssueKeyRegex = new(@"\[(?<key>[A-Z][A-Z0-9]+-\d+)\]", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IntegrationConfigStore _configStore;
    private readonly IProjectRepository _projects;
    private readonly IIssueRepository _issues;
    private readonly IActivityLogRepository _activityLogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<GitHubIntegrationService> _logger;

    public GitHubIntegrationService(
        IntegrationConfigStore configStore,
        IProjectRepository projects,
        IIssueRepository issues,
        IActivityLogRepository activityLogs,
        IUnitOfWork unitOfWork,
        IPermissionService permissionService,
        ICurrentUserContext currentUserContext,
        ILogger<GitHubIntegrationService>? logger = null)
    {
        _configStore = configStore;
        _projects = projects;
        _issues = issues;
        _activityLogs = activityLogs;
        _unitOfWork = unitOfWork;
        _permissionService = permissionService;
        _currentUserContext = currentUserContext;
        _logger = logger ?? NullLogger<GitHubIntegrationService>.Instance;
    }

    public Task<GitHubProjectConfig?> GetConfigAsync(int projectId, CancellationToken cancellationToken = default)
    {
        return _configStore.GetAsync<GitHubProjectConfig>(projectId, IntegrationNames.GitHub, cancellationToken);
    }

    public async Task ConfigureAsync(int projectId, GitHubProjectConfig config, bool isEnabled = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        await EnsureManageProjectPermissionAsync(projectId, cancellationToken);

        var normalized = new GitHubProjectConfig(
            NormalizeRequired(config.Owner, "GitHub owner"),
            NormalizeRequired(config.Repo, "GitHub repository"),
            NormalizeRequired(config.ApiToken, "GitHub API token"));

        await _configStore.SaveAsync(projectId, IntegrationNames.GitHub, normalized, isEnabled, cancellationToken: cancellationToken);
        _logger.LogInformation("Configured GitHub integration for project {ProjectId} ({Owner}/{Repo}).", projectId, normalized.Owner, normalized.Repo);
    }

    public async Task DisconnectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        await EnsureManageProjectPermissionAsync(projectId, cancellationToken);
        await _configStore.RemoveAsync(projectId, IntegrationNames.GitHub, cancellationToken);
        _logger.LogInformation("Disconnected GitHub integration for project {ProjectId}.", projectId);
    }

    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var configs = await _configStore.GetEnabledAsync(IntegrationNames.GitHub, cancellationToken);
        foreach (var config in configs)
        {
            try
            {
                await SyncProjectAsync(config.ProjectId, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "GitHub sync failed for project {ProjectId}.", config.ProjectId);
            }
        }
    }

    public async Task SyncProjectAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var configEntity = await _configStore.GetEntityAsync(projectId, IntegrationNames.GitHub, cancellationToken);
        var config = await GetConfigAsync(projectId, cancellationToken);
        if (configEntity is null || !configEntity.IsEnabled || config is null)
        {
            return;
        }

        var project = await _projects.GetByIdAsync(projectId, cancellationToken)
            ?? throw new ValidationException($"Project {projectId} was not found.");
        var issues = await _issues.GetAllByProjectIdAsync(projectId, cancellationToken);
        var issueLookup = issues.ToDictionary(issue => issue.IssueKey.ToUpperInvariant(), issue => issue);
        if (issueLookup.Count == 0)
        {
            await _configStore.UpdateLastSyncAsync(projectId, IntegrationNames.GitHub, DateTime.UtcNow, cancellationToken);
            return;
        }

        var client = CreateClient(config.ApiToken);
        var nowUtc = DateTime.UtcNow;
        var sinceUtc = configEntity.LastSyncAtUtc?.AddMinutes(-1);
        var addedEntries = 0;

        var commitRequest = new CommitRequest();
        if (sinceUtc.HasValue)
        {
            commitRequest.Since = sinceUtc.Value;
        }

        var commits = await client.Repository.Commit.GetAll(config.Owner, config.Repo, commitRequest);
        foreach (var commit in commits)
        {
            var issueKeys = ExtractIssueKeys(commit.Commit.Message);
            foreach (var issueKey in issueKeys)
            {
                if (!issueLookup.TryGetValue(issueKey, out var issue))
                {
                    continue;
                }

                var commitUrl = commit.HtmlUrl ?? $"https://github.com/{config.Owner}/{config.Repo}/commit/{commit.Sha}";
                if (await _activityLogs.ExistsIssueEntryAsync(issue.Id, CommitFieldName, commitUrl, cancellationToken))
                {
                    continue;
                }

                await _activityLogs.AddAsync(new ActivityLogEntity
                {
                    ProjectId = projectId,
                    IssueId = issue.Id,
                    UserId = issue.CreatedById,
                    ActionType = ActivityActionType.Updated,
                    FieldName = CommitFieldName,
                    NewValue = commitUrl,
                    OccurredAtUtc = commit.Commit.Author.Date.UtcDateTime,
                    MetadataJson = JsonSerializer.Serialize(new GitHubCommitMetadata(
                        commit.Sha[..Math.Min(7, commit.Sha.Length)],
                        TrimText(commit.Commit.Message),
                        commit.Commit.Author.Name ?? commit.Author?.Login ?? "Unknown",
                        commit.Commit.Author.Date.UtcDateTime,
                        commitUrl), JsonOptions)
                }, cancellationToken);
                addedEntries++;
            }
        }

        var pullRequests = await client.PullRequest.GetAllForRepository(
            config.Owner,
            config.Repo,
            new PullRequestRequest
            {
                State = ItemStateFilter.All,
                SortProperty = PullRequestSort.Updated,
                SortDirection = SortDirection.Descending
            });

        foreach (var pullRequest in pullRequests.Where(pr => !sinceUtc.HasValue || pr.UpdatedAt.UtcDateTime >= sinceUtc.Value))
        {
            var issueKeys = ExtractIssueKeys($"{pullRequest.Title}\n{pullRequest.Body}");
            foreach (var issueKey in issueKeys)
            {
                if (!issueLookup.TryGetValue(issueKey, out var issue))
                {
                    continue;
                }

                var pullRequestUrl = pullRequest.HtmlUrl ?? $"https://github.com/{config.Owner}/{config.Repo}/pull/{pullRequest.Number}";
                if (await _activityLogs.ExistsIssueEntryAsync(issue.Id, PullRequestFieldName, pullRequestUrl, cancellationToken))
                {
                    continue;
                }

                await _activityLogs.AddAsync(new ActivityLogEntity
                {
                    ProjectId = projectId,
                    IssueId = issue.Id,
                    UserId = issue.CreatedById,
                    ActionType = ActivityActionType.Updated,
                    FieldName = PullRequestFieldName,
                    NewValue = pullRequestUrl,
                    OccurredAtUtc = pullRequest.UpdatedAt.UtcDateTime,
                    MetadataJson = JsonSerializer.Serialize(new GitHubPullRequestMetadata(
                        pullRequest.Number,
                        TrimText(pullRequest.Title, 240),
                        pullRequest.User?.Login ?? "Unknown",
                        pullRequest.State.StringValue,
                        pullRequest.UpdatedAt.UtcDateTime,
                        pullRequestUrl), JsonOptions)
                }, cancellationToken);
                addedEntries++;
            }
        }

        if (addedEntries > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("GitHub sync linked {Count} commit/PR entries for project {ProjectId}.", addedEntries, projectId);
        }

        await _configStore.UpdateLastSyncAsync(projectId, IntegrationNames.GitHub, nowUtc, cancellationToken);
    }

    public async Task<IReadOnlyList<GitHubCommitLinkDto>> GetIssueCommitsAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var activity = await _activityLogs.GetIssueActivityAsync(issueId, cancellationToken);
        return activity
            .Where(x => x.FieldName == CommitFieldName)
            .Select(x => Deserialize<GitHubCommitMetadata>(x.MetadataJson))
            .Where(x => x is not null)
            .Select(x => new GitHubCommitLinkDto(x!.Sha, x.Message, x.Author, x.TimestampUtc, x.Url))
            .OrderByDescending(x => x.TimestampUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<GitHubPullRequestLinkDto>> GetIssuePullRequestsAsync(int issueId, CancellationToken cancellationToken = default)
    {
        var activity = await _activityLogs.GetIssueActivityAsync(issueId, cancellationToken);
        return activity
            .Where(x => x.FieldName == PullRequestFieldName)
            .Select(x => Deserialize<GitHubPullRequestMetadata>(x.MetadataJson))
            .Where(x => x is not null)
            .Select(x => new GitHubPullRequestLinkDto(x!.Number, x.Title, x.Author, x.State, x.UpdatedAtUtc, x.Url))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToList();
    }

    private async Task EnsureManageProjectPermissionAsync(int projectId, CancellationToken cancellationToken)
    {
        var currentUserId = _currentUserContext.RequireUserId();
        if (!await _permissionService.HasPermissionAsync(currentUserId, projectId, Permission.ManageProject, cancellationToken))
        {
            throw new UnauthorizedAccessException("Current user does not have permission to manage project integrations.");
        }
    }

    private static GitHubClient CreateClient(string apiToken)
    {
        var client = new GitHubClient(new ProductHeaderValue("JiraDesktop"));
        client.Credentials = new Credentials(apiToken);
        return client;
    }

    private static HashSet<string> ExtractIssueKeys(string? text)
    {
        return IssueKeyRegex.Matches(text ?? string.Empty)
            .Select(match => match.Groups["key"].Value.ToUpperInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet();
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string TrimText(string? value, int maxLength = 600)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength];
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

    private sealed record GitHubCommitMetadata(string Sha, string Message, string Author, DateTime TimestampUtc, string Url);
    private sealed record GitHubPullRequestMetadata(int Number, string Title, string Author, string State, DateTime UpdatedAtUtc, string Url);
}
