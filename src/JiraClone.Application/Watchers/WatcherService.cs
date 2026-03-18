using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Watchers;

public class WatcherService : IWatcherService
{
    private readonly IWatcherRepository _watchers;
    private readonly IIssueRepository _issues;
    private readonly IUserRepository _users;
    private readonly IAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WatcherService> _logger;

    public WatcherService(
        IWatcherRepository watchers,
        IIssueRepository issues,
        IUserRepository users,
        IAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        ILogger<WatcherService>? logger = null)
    {
        _watchers = watchers;
        _issues = issues;
        _users = users;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<WatcherService>.Instance;
    }

    public async Task<bool> WatchIssueAsync(int issueId, int userId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(JiraClone.Application.Roles.RoleCatalog.Admin, JiraClone.Application.Roles.RoleCatalog.ProjectManager, JiraClone.Application.Roles.RoleCatalog.Developer, JiraClone.Application.Roles.RoleCatalog.Viewer);
        if (await _watchers.GetAsync(issueId, userId, cancellationToken) is not null)
        {
            return false;
        }

        var issue = await _issues.GetByIdAsync(issueId, cancellationToken);
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (issue is null || user is null)
        {
            return false;
        }

        await _watchers.AddAsync(new Watcher
        {
            IssueId = issueId,
            Issue = issue,
            UserId = userId,
            User = user,
            WatchedAtUtc = DateTime.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} started watching issue {IssueId}.", userId, issueId);
        return true;
    }

    public async Task<bool> UnwatchIssueAsync(int issueId, int userId, CancellationToken cancellationToken = default)
    {
        _authorization.EnsureInRole(JiraClone.Application.Roles.RoleCatalog.Admin, JiraClone.Application.Roles.RoleCatalog.ProjectManager, JiraClone.Application.Roles.RoleCatalog.Developer, JiraClone.Application.Roles.RoleCatalog.Viewer);
        var watcher = await _watchers.GetAsync(issueId, userId, cancellationToken);
        if (watcher is null)
        {
            return false;
        }

        await _watchers.RemoveAsync(watcher, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} stopped watching issue {IssueId}.", userId, issueId);
        return true;
    }

    public async Task<IReadOnlyList<User>> GetWatchersAsync(int issueId, CancellationToken cancellationToken = default) =>
        (await _watchers.GetByIssueIdAsync(issueId, cancellationToken)).Select(x => x.User).ToList();

    public async Task<bool> IsWatchingAsync(int issueId, int userId, CancellationToken cancellationToken = default) =>
        await _watchers.GetAsync(issueId, userId, cancellationToken) is not null;
}

