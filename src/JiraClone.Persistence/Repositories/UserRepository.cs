using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public UserRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

    public Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default) =>
        _dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserName == userName, cancellationToken);

    public Task<User?> GetByIdAsync(int userId, CancellationToken cancellationToken = default) =>
        _dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Include(x => x.ProjectMemberships)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public async Task<IReadOnlyList<User>> GetProjectUsersAsync(int projectId, CancellationToken cancellationToken = default) =>
        await _dbContext.ProjectMembers
            .Where(x => x.ProjectId == projectId)
            .Include(x => x.User)
            .ThenInclude(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Select(x => x.User)
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Role>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Roles
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        _dbContext.Users.AddAsync(user, cancellationToken).AsTask();
}
