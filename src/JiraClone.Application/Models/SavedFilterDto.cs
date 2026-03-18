namespace JiraClone.Application.Models;

public sealed record SavedFilterDto(int Id, int ProjectId, int UserId, string Name, string QueryText, bool IsFavorite);
