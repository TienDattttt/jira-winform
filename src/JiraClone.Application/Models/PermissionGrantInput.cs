using JiraClone.Domain.Enums;

namespace JiraClone.Application.Models;

public sealed record PermissionGrantInput(Permission Permission, ProjectRole ProjectRole);
