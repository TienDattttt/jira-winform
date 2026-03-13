using JiraClone.Application.Abstractions;

namespace JiraClone.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly JiraCloneDbContext _dbContext;

    public UnitOfWork(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
