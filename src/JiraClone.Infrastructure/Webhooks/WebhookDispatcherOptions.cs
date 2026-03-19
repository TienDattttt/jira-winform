namespace JiraClone.Infrastructure.Webhooks;

public sealed class WebhookDispatcherOptions
{
    public int QueueCapacity { get; init; } = 500;

    public int MaxAttempts { get; init; } = 3;

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public IReadOnlyList<TimeSpan> RetryDelays { get; init; } =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2)
    ];
}
