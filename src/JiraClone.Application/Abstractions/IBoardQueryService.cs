using JiraClone.Application.Models;

namespace JiraClone.Application.Abstractions;

public interface IBoardQueryService
{
    Task<IReadOnlyList<BoardColumnDto>> GetBoardAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BoardColumnDto>> GetBoardAsync(int projectId, int? sprintId, CancellationToken cancellationToken = default);
}
