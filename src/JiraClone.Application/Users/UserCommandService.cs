using System.ComponentModel.DataAnnotations;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Auth;
using JiraClone.Application.Roles;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using ActivityLogEntity = JiraClone.Domain.Entities.ActivityLog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Users;

public class UserCommandService
{
    private readonly IUserRepository _users;
    private readonly IProjectRepository _projects;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthorizationService _authorization;
    private readonly IActivityLogRepository _activityLogs;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserCommandService> _logger;

    public UserCommandService(
        IUserRepository users,
        IProjectRepository projects,
        IPasswordHasher passwordHasher,
        IAuthorizationService authorization,
        IActivityLogRepository activityLogs,
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork,
        ILogger<UserCommandService>? logger = null)
    {
        _users = users;
        _projects = projects;
        _passwordHasher = passwordHasher;
        _authorization = authorization;
        _activityLogs = activityLogs;
        _currentUserContext = currentUserContext;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<UserCommandService>.Instance;
    }

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading all users.");
        return _users.GetAllAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading available roles.");
        return _users.GetRolesAsync(cancellationToken);
    }

    public async Task<User> CreateAsync(
        int projectId,
        string userName,
        string displayName,
        string email,
        string password,
        ProjectRole projectRole,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating user {UserName} in project {ProjectId}.", userName, projectId);
        _authorization.EnsureInRole(RoleCatalog.Admin);
        AuthenticationService.EnsurePasswordValid(password);

        var (hash, salt) = _passwordHasher.Hash(password);
        var user = new User
        {
            UserName = userName.Trim(),
            DisplayName = displayName.Trim(),
            Email = email.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true
        };

        var roles = await _users.GetRolesAsync(cancellationToken);
        foreach (var roleName in roleNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var role = roles.FirstOrDefault(x => x.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));
            if (role is null)
            {
                continue;
            }

            user.UserRoles.Add(new UserRole { User = user, RoleId = role.Id, Role = role });
        }

        await _users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var project = await _projects.GetByIdAsync(projectId, cancellationToken);
        if (project is not null)
        {
            project.Members.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = user.Id,
                User = user,
                ProjectRole = projectRole,
                JoinedAtUtc = DateTime.UtcNow
            });
            await AddUserActivityAsync(project.Id, ActivityActionType.Created, nameof(User.UserName), null, user.UserName, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    public async Task<User?> UpdateAsync(
        int userId,
        string displayName,
        string email,
        bool isActive,
        ProjectRole? projectRole,
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating user {UserId}.", userId);
        _authorization.EnsureInRole(RoleCatalog.Admin);
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("User {UserId} was not found for update.", userId);
            return null;
        }

        var oldDisplayName = user.DisplayName;
        user.DisplayName = displayName.Trim();
        user.Email = email.Trim();
        user.IsActive = isActive;
        if (!isActive)
        {
            user.LastRefreshToken = null;
            user.SessionExpiresAtUtc = null;
        }

        user.UpdatedAtUtc = DateTime.UtcNow;

        user.UserRoles.Clear();
        var roles = await _users.GetRolesAsync(cancellationToken);
        foreach (var roleName in roleNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var role = roles.FirstOrDefault(x => x.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));
            if (role is null)
            {
                continue;
            }

            user.UserRoles.Add(new UserRole { UserId = user.Id, User = user, RoleId = role.Id, Role = role });
        }

        if (projectRole.HasValue)
        {
            foreach (var membership in user.ProjectMemberships)
            {
                membership.ProjectRole = projectRole.Value;
            }
        }

        await AddUserActivityAsync(GetAuditProjectId(user), ActivityActionType.Updated, nameof(User.DisplayName), oldDisplayName, user.DisplayName, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<bool> ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting password for user {UserId}.", userId);
        _authorization.EnsureInRole(RoleCatalog.Admin);
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("User {UserId} was not found for password reset.", userId);
            return false;
        }

        AuthenticationService.EnsurePasswordValid(newPassword);
        var (hash, salt) = _passwordHasher.Hash(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.LastRefreshToken = null;
        user.SessionExpiresAtUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await AddUserActivityAsync(GetAuditProjectId(user), ActivityActionType.Updated, nameof(User.PasswordHash), "***", "***", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<User?> UpdateEmailNotificationsPreferenceAsync(int userId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating email notification preference for user {UserId} to {IsEnabled}.", userId, isEnabled);
        var currentUser = _currentUserContext.CurrentUser
            ?? throw new InvalidOperationException("Không có người dùng nào đang đăng nhập.");

        if (currentUser.Id != userId)
        {
            _authorization.EnsureInRole(RoleCatalog.Admin);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("User {UserId} was not found while updating email notification preference.", userId);
            return null;
        }

        if (user.EmailNotificationsEnabled == isEnabled)
        {
            if (currentUser.Id == user.Id)
            {
                _currentUserContext.Set(user);
            }

            return user;
        }

        var oldValue = user.EmailNotificationsEnabled.ToString();
        user.EmailNotificationsEnabled = isEnabled;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await AddUserActivityAsync(
            GetAuditProjectId(user),
            ActivityActionType.Updated,
            nameof(User.EmailNotificationsEnabled),
            oldValue,
            isEnabled.ToString(),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (currentUser.Id == user.Id)
        {
            _currentUserContext.Set(user);
        }

        return user;
    }

    public Task<User?> DeactivateAsync(int userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deactivating user {UserId}.", userId);
        return UpdateStatusAsync(userId, false, cancellationToken);
    }

    public Task<User?> ActivateAsync(int userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Activating user {UserId}.", userId);
        return UpdateStatusAsync(userId, true, cancellationToken);
    }

    private async Task<User?> UpdateStatusAsync(int userId, bool isActive, CancellationToken cancellationToken)
    {
        _authorization.EnsureInRole(RoleCatalog.Admin);
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("User {UserId} was not found while changing active state to {IsActive}.", userId, isActive);
            return null;
        }

        user.IsActive = isActive;
        if (!isActive)
        {
            user.LastRefreshToken = null;
            user.SessionExpiresAtUtc = null;
        }

        user.UpdatedAtUtc = DateTime.UtcNow;
        await AddUserActivityAsync(GetAuditProjectId(user), ActivityActionType.Updated, nameof(User.IsActive), (!isActive).ToString(), isActive.ToString(), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task AddUserActivityAsync(int? projectId, ActivityActionType actionType, string? fieldName, string? oldValue, string? newValue, CancellationToken cancellationToken)
    {
        var actorUserId = _currentUserContext.CurrentUser?.Id;
        if (!projectId.HasValue || !actorUserId.HasValue)
        {
            return;
        }

        await _activityLogs.AddAsync(new ActivityLogEntity
        {
            ProjectId = projectId.Value,
            UserId = actorUserId.Value,
            ActionType = actionType,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue
        }, cancellationToken);
    }

    private static int? GetAuditProjectId(User user) => user.ProjectMemberships.FirstOrDefault()?.ProjectId;
}
