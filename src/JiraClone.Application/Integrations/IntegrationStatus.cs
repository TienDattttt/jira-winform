namespace JiraClone.Application.Integrations;

public sealed record IntegrationStatus(
    string Name,
    string Description,
    bool IsConfigured,
    bool IsEnabled,
    string BadgeText,
    DateTime? LastSyncAtUtc,
    string? Detail = null);
