using JiraClone.Application.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Infrastructure.Integrations;

public sealed class GitHubIntegrationSyncWorker : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GitHubIntegrationSyncWorker> _logger;
    private readonly Timer _timer;

    public GitHubIntegrationSyncWorker(IServiceScopeFactory scopeFactory, ILogger<GitHubIntegrationSyncWorker>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger ?? NullLogger<GitHubIntegrationSyncWorker>.Instance;
        _timer = new Timer(async _ => await TickAsync(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(15));
    }

    private async Task TickAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IGitHubIntegrationService>();
            await service.SyncAllAsync();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "GitHub integration background sync tick failed.");
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
