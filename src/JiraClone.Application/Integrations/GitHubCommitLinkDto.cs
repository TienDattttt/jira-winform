namespace JiraClone.Application.Integrations;

public sealed record GitHubCommitLinkDto(string Sha, string Message, string Author, DateTime TimestampUtc, string Url);
