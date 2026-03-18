namespace JiraClone.Application.Integrations;

public sealed record GitHubPullRequestLinkDto(int Number, string Title, string Author, string State, DateTime UpdatedAtUtc, string Url);
