using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JiraClone.Persistence.Repositories;

public class LabelRepository : ILabelRepository
{
    private readonly JiraCloneDbContext _dbContext;

    public LabelRepository(JiraCloneDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Label>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default) =>
        await _dbContext.Labels
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public Task<Label?> GetByIdAsync(int labelId, CancellationToken cancellationToken = default) =>
        _dbContext.Labels.FirstOrDefaultAsync(x => x.Id == labelId, cancellationToken);

    public Task AddAsync(Label label, CancellationToken cancellationToken = default) =>
        _dbContext.Labels.AddAsync(label, cancellationToken).AsTask();

    public Task RemoveAsync(Label label, CancellationToken cancellationToken = default)
    {
        _dbContext.Labels.Remove(label);
        return Task.CompletedTask;
    }
}
