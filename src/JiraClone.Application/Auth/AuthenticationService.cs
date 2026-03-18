using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using JiraClone.Application.Abstractions;
using JiraClone.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.Auth;

public class AuthenticationService
{
    private const int PersistentSessionLifetimeDays = 30;

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

    private static bool IsPasswordValid(string password) =>
        password.Length >= 8 &&
        password.Any(char.IsUpper) &&
        password.Any(char.IsDigit);

    private static string GenerateRefreshToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private static bool IsPersistentSessionValid(Domain.Entities.User? user, string refreshToken)
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

    private static void ClearPersistentSessionState(Domain.Entities.User user)
    {
        user.LastRefreshToken = null;
        user.SessionExpiresAtUtc = null;
    }
}

