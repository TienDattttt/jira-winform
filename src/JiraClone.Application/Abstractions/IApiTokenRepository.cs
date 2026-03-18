using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface IApiTokenRepository
{
    Task<ApiToken?> GetByIdAsync(int tokenId, CancellationToken cancellationToken = default);
    Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApiToken>> GetByUserAsync(int userId, CancellationToken cancellationToken = default);
    Task AddAsync(ApiToken token, CancellationToken cancellationToken = default);
}
