using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class ApiTokenScopeGrant
{
    public int ApiTokenId { get; set; }
    public ApiToken ApiToken { get; set; } = null!;
    public ApiTokenScope Scope { get; set; }
}
