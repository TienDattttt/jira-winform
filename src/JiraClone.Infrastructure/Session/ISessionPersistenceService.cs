using JiraClone.Application.Models;

namespace JiraClone.Infrastructure.Session;

public interface ISessionPersistenceService
{
    Task SaveAsync(SessionData session);
    Task<SessionData?> LoadAsync();
    Task ClearAsync();
}
