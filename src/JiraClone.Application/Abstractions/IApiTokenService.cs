using JiraClone.Application.ApiTokens;
using JiraClone.Domain.Entities;
using JiraClone.Domain.Enums;

namespace JiraClone.Application.Abstractions;

public interface IApiTokenService
{
    Task<GeneratedTokenResult> CreateTokenAsync(int userId, string name, DateTime? expiresAtUtc, IReadOnlyCollection<ApiTokenScope> scopes, CancellationToken cancellationToken = default);
    Task<ApiToken?> ValidateTokenAsync(string rawToken, CancellationToken cancellationToken = default);
    Task RevokeTokenAsync(int tokenId, int requestingUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApiToken>> GetUserTokensAsync(int userId, CancellationToken cancellationToken = default);
}
