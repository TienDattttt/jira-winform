namespace JiraClone.Application.Models;

public sealed record BoardColumnWipLimitEventArgs(int StatusId, string StatusName, int CurrentCount, int Limit);