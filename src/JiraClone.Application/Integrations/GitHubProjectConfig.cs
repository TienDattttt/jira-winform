namespace JiraClone.Application.Integrations;

public sealed record GitHubProjectConfig(string Owner, string Repo, string ApiToken);
