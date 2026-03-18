using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using JiraClone.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Auth;

public class AuthenticationService
{
    private const int PersistentSessionLifetimeDays = 30;
    private const string DefaultSsoRoleName = "Viewer";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ICurrentUserContext currentUserContext,
        IUnitOfWork unitOfWork,
        ILogger<AuthenticationService>? logger = null)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _currentUserContext = currentUserContext;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<AuthenticationService>.Instance;
    }

    public async Task<AuthResult> LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting authentication for user {UserName}.", userName);
        var user = await _users.GetByUserNameAsync(userName, cancellationToken);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Authentication failed for user {UserName}: user not found or inactive.", userName);
            return new AuthResult(false, "Invalid username or password.", null);
        }

        if (!_passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            _logger.LogWarning("Authentication failed for user {UserName}: invalid password.", userName);
            return new AuthResult(false, "Invalid username or password.", null);
        }

        _currentUserContext.Set(user);
        _logger.LogInformation("Authentication succeeded for user {UserId}.", user.Id);
        return new AuthResult(true, null, user);
    }

    public async Task<AuthResult> LoginWithSsoAsync(string email, string? displayName = null, string? suggestedUserName = null, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        _logger.LogInformation("Attempting SSO authentication for email {Email}.", normalizedEmail);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new AuthResult(false, "A valid email address is required for SSO login.", null);
        }

        var user = await _users.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is not null && !user.IsActive)
        {
            _logger.LogWarning("SSO authentication failed for email {Email}: user is inactive.", normalizedEmail);
            return new AuthResult(false, "This account is inactive.", null);
        }

        if (user is null)
        {
            user = await CreateSsoUserAsync(normalizedEmail, displayName, suggestedUserName, cancellationToken);
            _logger.LogInformation("Provisioned new local SSO user {UserId} for email {Email}.", user.Id, normalizedEmail);
        }
        else if (!string.IsNullOrWhiteSpace(displayName) && !string.Equals(user.DisplayName, displayName.Trim(), StringComparison.Ordinal))
        {
            user.DisplayName = displayName.Trim();
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _currentUserContext.Set(user);
        _logger.LogInformation("SSO authentication succeeded for user {UserId}.", user.Id);
        return new AuthResult(true, null, user);
    }

    public async Task<SessionData> CreatePersistentSessionAsync(int userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating persistent session for user {UserId}.", userId);
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Unable to create persistent session for user {UserId}: user not found or inactive.", userId);
            throw new InvalidOperationException("The user is not available for persistent sessions.");
        }

        var refreshToken = GenerateRefreshToken();
        user.LastRefreshToken = HashRefreshToken(refreshToken);
        user.SessionExpiresAtUtc = DateTime.UtcNow.AddDays(PersistentSessionLifetimeDays);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SessionData
        {
            UserId = user.Id,
            Username = user.UserName,
            ExpiresAtUtc = user.SessionExpiresAtUtc.Value,
            RefreshToken = refreshToken
        };
    }

    public async Task<bool> ValidateRefreshTokenAsync(int userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating persistent session for user {UserId}.", userId);
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (!IsPersistentSessionValid(user, refreshToken))
        {
            _currentUserContext.Clear();

            if (user is not null && (!string.IsNullOrWhiteSpace(user.LastRefreshToken) || user.SessionExpiresAtUtc.HasValue))
            {
                ClearPersistentSessionState(user);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            _logger.LogWarning("Persistent session validation failed for user {UserId}.", userId);
            return false;
        }

        _currentUserContext.Set(user!);
        _logger.LogInformation("Persistent session validation succeeded for user {UserId}.", userId);
        return true;
    }

    public async Task ClearPersistentSessionAsync(int userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Clearing persistent session for user {UserId}.", userId);
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is not null && (!string.IsNullOrWhiteSpace(user.LastRefreshToken) || user.SessionExpiresAtUtc.HasValue))
        {
            ClearPersistentSessionState(user);
            user.UpdatedAtUtc = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public static void EnsurePasswordValid(string? password)
    {
        var validationError = GetPasswordValidationError(password);
        if (validationError is not null)
        {
            throw new ValidationException(validationError);
        }
    }

    public static string? GetPasswordValidationError(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "Password is required.";
        }

        return IsPasswordValid(password)
            ? null
            : "Password must be at least 8 characters and include at least 1 uppercase letter and 1 number.";
    }

    private async Task<User> CreateSsoUserAsync(string normalizedEmail, string? displayName, string? suggestedUserName, CancellationToken cancellationToken)
    {
        var userName = await GenerateUniqueUserNameAsync(suggestedUserName, normalizedEmail, cancellationToken);
        var fallbackPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var (hash, salt) = _passwordHasher.Hash(fallbackPassword);
        var user = new User
        {
            UserName = userName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? userName : displayName.Trim(),
            Email = normalizedEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true,
            EmailNotificationsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var viewerRole = (await _users.GetRolesAsync(cancellationToken))
            .FirstOrDefault(role => role.Name.Equals(DefaultSsoRoleName, StringComparison.OrdinalIgnoreCase));
        if (viewerRole is not null)
        {
            user.UserRoles.Add(new UserRole
            {
                User = user,
                Role = viewerRole,
                RoleId = viewerRole.Id
            });
        }

        await _users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task<string> GenerateUniqueUserNameAsync(string? suggestedUserName, string normalizedEmail, CancellationToken cancellationToken)
    {
        var baseUserName = BuildBaseUserName(suggestedUserName, normalizedEmail);
        var candidate = baseUserName;
        var suffix = 1;

        while (await _users.GetByUserNameAsync(candidate, cancellationToken) is not null)
        {
            candidate = $"{baseUserName}{suffix}";
            if (candidate.Length > 100)
            {
                var trimmedBase = baseUserName[..Math.Min(baseUserName.Length, Math.Max(1, 100 - suffix.ToString().Length))];
                candidate = $"{trimmedBase}{suffix}";
            }

            suffix++;
        }

        return candidate;
    }

    private static string BuildBaseUserName(string? suggestedUserName, string normalizedEmail)
    {
        var raw = string.IsNullOrWhiteSpace(suggestedUserName)
            ? normalizedEmail.Split('@')[0]
            : suggestedUserName.Trim();

        var builder = new StringBuilder(raw.Length);
        foreach (var character in raw)
        {
            if (char.IsLetterOrDigit(character) || character is '.' or '_' or '-')
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length == 0 || builder[^1] != '.')
            {
                builder.Append('.');
            }
        }

        var normalized = builder.ToString().Trim('.', '-', '_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "sso.user";
        }

        return normalized.Length <= 100
            ? normalized
            : normalized[..100];
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static bool IsPasswordValid(string password) =>
        password.Length >= 8 &&
        password.Any(char.IsUpper) &&
        password.Any(char.IsDigit);

    private static string GenerateRefreshToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private static bool IsPersistentSessionValid(User? user, string refreshToken)
    {
        if (user is null ||
            !user.IsActive ||
            string.IsNullOrWhiteSpace(refreshToken) ||
            string.IsNullOrWhiteSpace(user.LastRefreshToken) ||
            !user.SessionExpiresAtUtc.HasValue ||
            user.SessionExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            return false;
        }

        try
        {
            var expectedHash = Convert.FromHexString(user.LastRefreshToken);
            var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
            return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void ClearPersistentSessionState(User user)
    {
        user.LastRefreshToken = null;
        user.SessionExpiresAtUtc = null;
    }
}
