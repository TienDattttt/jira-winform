namespace JiraClone.Application.Integrations;

public interface IGitHubIntegrationService
{
    Task<GitHubProjectConfig?> GetConfigAsync(int projectId, CancellationToken cancellationToken = default);
    Task ConfigureAsync(int projectId, GitHubProjectConfig config, bool isEnabled = true, CancellationToken cancellationToken = default);
    Task DisconnectAsync(int projectId, CancellationToken cancellationToken = default);
    Task SyncProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task SyncAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubCommitLinkDto>> GetIssueCommitsAsync(int issueId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitHubPullRequestLinkDto>> GetIssuePullRequestsAsync(int issueId, CancellationToken cancellationToken = default);
}
