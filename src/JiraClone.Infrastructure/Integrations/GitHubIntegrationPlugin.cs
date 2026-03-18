using JiraClone.Application.Integrations;

namespace JiraClone.Infrastructure.Integrations;

public sealed class GitHubIntegrationPlugin : IIntegrationPlugin
{
    private readonly IntegrationConfigStore _configStore;

    public GitHubIntegrationPlugin(IntegrationConfigStore configStore)
    {
        _configStore = configStore;
    }

    public string Name => IntegrationNames.GitHub;
    public string Description => "Link commits and pull requests from a GitHub repository to Jira issues.";

    public bool IsConfigured(int projectId)
    {
        return GetStatusAsync(projectId).GetAwaiter().GetResult().IsConfigured;
    }

    public async Task<IntegrationStatus> GetStatusAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var entity = await _configStore.GetEntityAsync(projectId, IntegrationNames.GitHub, cancellationToken);
        var config = await _configStore.GetAsync<GitHubProjectConfig>(projectId, IntegrationNames.GitHub, cancellationToken);
        var isConfigured = config is not null;
        var badge = !isConfigured ? "Disconnected" : entity?.IsEnabled == true ? "Connected" : "Disabled";
        var detail = config is null ? "No repository configured yet." : $"{config.Owner}/{config.Repo}";
        return new IntegrationStatus(Name, Description, isConfigured, entity?.IsEnabled ?? false, badge, entity?.LastSyncAtUtc, detail);
    }
}
