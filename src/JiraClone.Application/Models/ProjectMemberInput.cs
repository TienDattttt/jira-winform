using JiraClone.Domain.Enums;

namespace JiraClone.Application.Models;

public sealed record ProjectMemberInput(int UserId, ProjectRole ProjectRole);
