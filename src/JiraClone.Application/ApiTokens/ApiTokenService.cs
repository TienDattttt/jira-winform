using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JiraClone.Application.ApiTokens;

public sealed class ApiTokenService : IApiTokenService
{
    private const string TokenPrefix = "jdt_";

    private readonly IApiTokenRepository _tokens;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApiTokenService> _logger;

    public ApiTokenService(
        IApiTokenRepository tokens,
        IUserRepository users,
        IUnitOfWork unitOfWork,
        ILogger<ApiTokenService>? logger = null)
    {
        _tokens = tokens;
        _users = users;
        _unitOfWork = unitOfWork;
        _logger = logger ?? NullLogger<ApiTokenService>.Instance;
    }

    public async Task<GeneratedTokenResult> CreateTokenAsync(int userId, string name, DateTime? expiresAtUtc, IReadOnlyCollection<ApiTokenScope> scopes, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw new ValidationException("Không tìm thấy tài khoản người dùng.");
        if (!user.IsActive)
        {
            throw new ValidationException("Chỉ tài khoản đang hoạt động mới được phép tạo API token.");
        }

        var normalizedName = NormalizeName(name);
        var normalizedScopes = NormalizeScopes(scopes);
        var rawToken = GenerateRawToken();
        var token = new ApiToken
        {
            UserId = userId,
            User = user,
            Name = normalizedName,
            Label = normalizedName,
            TokenHash = HashToken(rawToken),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc?.ToUniversalTime(),
            IsRevoked = false,
        };

        foreach (var scope in normalizedScopes)
        {
            token.ScopeGrants.Add(new ApiTokenScopeGrant
            {
                ApiToken = token,
                Scope = scope,
            });
        }

        await _tokens.AddAsync(token, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created API token {ApiTokenId} for user {UserId} with {ScopeCount} scopes.", token.Id, userId, token.ScopeGrants.Count);
        return new GeneratedTokenResult(token.Id, rawToken);
    }

    public async Task<ApiToken?> ValidateTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (!LooksLikeApiToken(rawToken))
        {
            return null;
        }

        var token = await _tokens.GetByHashAsync(HashToken(rawToken), cancellationToken);
        if (token is null || token.IsRevoked || token.User is null || !token.User.IsActive)
        {
            return null;
        }

        if (token.ExpiresAtUtc.HasValue && token.ExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            return null;
        }

        token.LastUsedAtUtc = DateTime.UtcNow;
        token.UpdatedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task RevokeTokenAsync(int tokenId, int requestingUserId, CancellationToken cancellationToken = default)
    {
        var token = await _tokens.GetByIdAsync(tokenId, cancellationToken);
        if (token is null)
        {
            return;
        }

        var requestor = await _users.GetByIdAsync(requestingUserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Không tìm thấy người dùng gửi yêu cầu.");
        if (requestor.Id != token.UserId && requestor.UserRoles.All(role => !role.Role.Name.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException("Chỉ chủ sở hữu Token hoặc Quản trị viên mới có quyền thu hồi Token này.");
        }

        if (token.IsRevoked)
        {
            return;
        }

        token.IsRevoked = true;
        token.UpdatedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Revoked API token {ApiTokenId} by user {RequestingUserId}.", tokenId, requestingUserId);
    }

    public Task<IReadOnlyList<ApiToken>> GetUserTokensAsync(int userId, CancellationToken cancellationToken = default) =>
        _tokens.GetByUserAsync(userId, cancellationToken);

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Tên API Token không được để trống.");
        }

        return name.Trim();
    }

    private static IReadOnlyList<ApiTokenScope> NormalizeScopes(IReadOnlyCollection<ApiTokenScope> scopes)
    {
        var normalizedScopes = (scopes ?? Array.Empty<ApiTokenScope>())
            .Distinct()
            .OrderBy(scope => scope)
            .ToList();
        if (normalizedScopes.Count == 0)
        {
            throw new ValidationException("Vui lòng chọn ít nhất một quyền (scope) cho API Token.");
        }

        return normalizedScopes;
    }

    private static bool LooksLikeApiToken(string rawToken) =>
        !string.IsNullOrWhiteSpace(rawToken) && rawToken.StartsWith(TokenPrefix, StringComparison.Ordinal) && rawToken.Length > TokenPrefix.Length + 10;

    private static string GenerateRawToken() =>
        TokenPrefix + Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken.Trim())));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
