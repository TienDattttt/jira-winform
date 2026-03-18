using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;

namespace JiraClone.Application.Integrations;

public class IntegrationConfigStore
{
    private readonly IProjectIntegrationConfigRepository _configs;
    private readonly IIntegrationConfigProtector _protector;
    private readonly IUnitOfWork _unitOfWork;

    public IntegrationConfigStore(
        IProjectIntegrationConfigRepository configs,
        IIntegrationConfigProtector protector,
        IUnitOfWork unitOfWork)
    {
        _configs = configs;
        _protector = protector;
        _unitOfWork = unitOfWork;
    }

    public async Task<TConfig?> GetAsync<TConfig>(int projectId, string integrationName, CancellationToken cancellationToken = default)
        where TConfig : class
    {
        var entity = await _configs.GetByProjectAndNameAsync(projectId, integrationName, cancellationToken);
        if (entity is null || string.IsNullOrWhiteSpace(entity.ConfigJson))
        {
            return null;
        }

        return _protector.Unprotect<TConfig>(entity.ConfigJson);
    }

    public Task<ProjectIntegrationConfig?> GetEntityAsync(int projectId, string integrationName, CancellationToken cancellationToken = default)
    {
        return _configs.GetByProjectAndNameAsync(projectId, integrationName, cancellationToken);
    }

    public Task<IReadOnlyList<ProjectIntegrationConfig>> GetEnabledAsync(string integrationName, CancellationToken cancellationToken = default)
    {
        return _configs.GetEnabledByIntegrationNameAsync(integrationName, cancellationToken);
    }

    public async Task SaveAsync<TConfig>(int projectId, string integrationName, TConfig config, bool isEnabled = true, DateTime? lastSyncAtUtc = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        var entity = await _configs.GetByProjectAndNameAsync(projectId, integrationName, cancellationToken);
        if (entity is null)
        {
            entity = new ProjectIntegrationConfig
            {
                ProjectId = projectId,
                IntegrationName = integrationName,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            };
            await _configs.AddAsync(entity, cancellationToken);
        }

        entity.ConfigJson = _protector.Protect(config);
        entity.IsEnabled = isEnabled;
        entity.LastSyncAtUtc = lastSyncAtUtc ?? entity.LastSyncAtUtc;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLastSyncAsync(int projectId, string integrationName, DateTime lastSyncAtUtc, CancellationToken cancellationToken = default)
    {
        var entity = await _configs.GetByProjectAndNameAsync(projectId, integrationName, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.LastSyncAtUtc = lastSyncAtUtc;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(int projectId, string integrationName, CancellationToken cancellationToken = default)
    {
        var entity = await _configs.GetByProjectAndNameAsync(projectId, integrationName, cancellationToken);
        if (entity is null)
        {
            return;
        }

        await _configs.RemoveAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
