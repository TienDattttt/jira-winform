namespace JiraClone.Application.Integrations;

public interface IIntegrationPlugin
{
    string Name { get; }
    string Description { get; }
    bool IsConfigured(int projectId);
    Task<IntegrationStatus> GetStatusAsync(int projectId, CancellationToken cancellationToken = default);
}
