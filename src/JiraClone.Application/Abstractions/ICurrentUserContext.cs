using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface ICurrentUserContext
{
    User? CurrentUser { get; }
    int RequireUserId();
    void Set(User user);
    void Clear();
}

