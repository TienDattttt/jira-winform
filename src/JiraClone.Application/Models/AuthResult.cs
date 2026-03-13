using JiraClone.Domain.Entities;

namespace JiraClone.Application.Models;

public sealed record AuthResult(bool Succeeded, string? ErrorMessage, User? User);
