using JiraClone.Domain.Entities;

namespace JiraClone.Application.Abstractions;

public interface ICurrentUserContext
{
    User? CurrentUser { get; }
    void Set(User user);
    void Clear();
}
