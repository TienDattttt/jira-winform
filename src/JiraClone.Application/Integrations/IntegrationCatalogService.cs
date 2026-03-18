namespace JiraClone.Application.Integrations;

public class IntegrationCatalogService : IIntegrationCatalogService
{
    private readonly IEnumerable<IIntegrationPlugin> _plugins;

    public IntegrationCatalogService(IEnumerable<IIntegrationPlugin> plugins)
    {
        _plugins = plugins;
    }

    public async Task<IReadOnlyList<IntegrationStatus>> GetProjectStatusesAsync(int projectId, CancellationToken cancellationToken = default)
    {
        var tasks = _plugins
            .OrderBy(plugin => plugin.Name)
            .Select(plugin => plugin.GetStatusAsync(projectId, cancellationToken));

        return await Task.WhenAll(tasks);
    }
}
