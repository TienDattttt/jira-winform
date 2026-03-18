using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class ApiTokenRepository : IApiTokenRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public ApiTokenRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ApiToken?> GetByIdAsync(int tokenId, CancellationToken cancellationToken = default) =>
        QueryTokens().FirstOrDefaultAsync(token => token.Id == tokenId, cancellationToken);

    public Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        QueryTokens().FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<ApiToken>> GetByUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await QueryTokens()
            .Where(token => token.UserId == userId)
            .OrderByDescending(token => token.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public Task AddAsync(ApiToken token, CancellationToken cancellationToken = default) =>
        _dbContext.Set<ApiToken>().AddAsync(token, cancellationToken).AsTask();

    private IQueryable<ApiToken> QueryTokens() =>
        _dbContext.Set<ApiToken>()
            .Include(token => token.User)
                .ThenInclude(user => user.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
            .Include(token => token.ScopeGrants);
}
