namespace JiraClone.Application.Abstractions;

public interface IWebhookSecretProtector
{
    string Protect(string secret);
    string Unprotect(string protectedSecret);
}
