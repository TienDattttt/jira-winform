using JiraClone.Domain.Common;
using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class ApiToken : AggregateRoot
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    public bool IsRevoked { get; set; }
    public ICollection<ApiTokenScopeGrant> ScopeGrants { get; set; } = new List<ApiTokenScopeGrant>();

    public IReadOnlyList<ApiTokenScope> Scopes => ScopeGrants
        .Select(scopeGrant => scopeGrant.Scope)
        .Distinct()
        .OrderBy(scope => scope)
        .ToList();
}
