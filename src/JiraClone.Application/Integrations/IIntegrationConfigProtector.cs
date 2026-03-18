namespace JiraClone.Application.Integrations;

public interface IIntegrationConfigProtector
{
    string Protect<TConfig>(TConfig config);
    TConfig? Unprotect<TConfig>(string protectedValue);
}
