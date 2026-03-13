using JiraClone.Application.Abstractions;
using JiraClone.Domain.Entities;

namespace JiraClone.Infrastructure.Security;

public class CurrentUserContext : ICurrentUserContext
{
    public User? CurrentUser { get; private set; }

    public void Set(User user) => CurrentUser = user;

    public void Clear() => CurrentUser = null;
}
