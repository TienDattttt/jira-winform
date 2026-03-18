namespace JiraClone.Application.Integrations;

public interface IIntegrationCatalogService
{
    Task<IReadOnlyList<IntegrationStatus>> GetProjectStatusesAsync(int projectId, CancellationToken cancellationToken = default);
}
