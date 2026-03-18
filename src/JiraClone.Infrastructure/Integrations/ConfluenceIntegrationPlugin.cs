using JiraClone.Application.Integrations;

namespace JiraClone.Infrastructure.Integrations;

public sealed class ConfluenceIntegrationPlugin : IIntegrationPlugin
{
    private readonly IntegrationConfigStore _configStore;

    public ConfluenceIntegrationPlugin(IntegrationConfigStore configStore)
    {
        _configStore = configStore;
    }

    public string Name => IntegrationNames.Confluence;
    public string Description => "Create and link Confluence pages directly from issue details.";

    public bool IsConfigured(int projectId)
    {
        return GetStatusAsync(projectId).GetAwaiter().GetResult().IsConfigured;
    }

    public async Task<IntegrationStatus> GetStatusAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var entity = await _configStore.GetEntityAsync(projectId, IntegrationNames.Confluence, cancellationToken);
        var config = await _configStore.GetAsync<ConfluenceProjectConfig>(projectId, IntegrationNames.Confluence, cancellationToken);
        var isConfigured = config is not null;
        var badge = !isConfigured ? "Disconnected" : entity?.IsEnabled == true ? "Connected" : "Disabled";
        var detail = config is null ? "No Confluence space configured yet." : $"{config.SpaceKey} at {config.BaseUrl}";
        return new IntegrationStatus(Name, Description, isConfigured, entity?.IsEnabled ?? false, badge, entity?.LastSyncAtUtc, detail);
    }
}
