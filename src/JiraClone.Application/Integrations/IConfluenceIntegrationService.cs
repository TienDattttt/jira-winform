namespace JiraClone.Application.Integrations;

public interface IConfluenceIntegrationService
{
    Task<ConfluenceProjectConfig?> GetConfigAsync(int projectId, CancellationToken cancellationToken = default);
    Task ConfigureAsync(int projectId, ConfluenceProjectConfig config, bool isEnabled = true, CancellationToken cancellationToken = default);
    Task DisconnectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConfluencePageLinkDto>> GetIssuePagesAsync(int issueId, CancellationToken cancellationToken = default);
    Task AddPageLinkAsync(int issueId, string title, string url, int userId, CancellationToken cancellationToken = default);
    Task<ConfluencePageLinkDto> CreatePageFromIssueAsync(int issueId, int userId, CancellationToken cancellationToken = default);
}
