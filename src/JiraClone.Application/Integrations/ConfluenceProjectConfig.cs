namespace JiraClone.Application.Integrations;

public sealed record ConfluenceProjectConfig(string BaseUrl, string SpaceKey, string ApiToken, string Email);
