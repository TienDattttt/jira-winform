using JiraClone.Application.Models;

namespace JiraClone.Application.Abstractions;

public interface IRoadmapService
{
    Task<IReadOnlyList<RoadmapEpicDto>> GetEpicsForRoadmapAsync(int projectId, CancellationToken cancellationToken = default);
}