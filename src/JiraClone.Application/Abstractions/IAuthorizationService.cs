namespace JiraClone.Application.Abstractions;

public interface IAuthorizationService
{
    bool IsInRole(params string[] roleNames);
    void EnsureInRole(params string[] roleNames);
}
