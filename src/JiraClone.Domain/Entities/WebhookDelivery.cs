using JiraClone.Domain.Common;
using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class WebhookDelivery : AggregateRoot
{
    public int WebhookEndpointId { get; set; }
    public WebhookEndpoint WebhookEndpoint { get; set; } = null!;
    public WebhookEventType EventType { get; set; }
    public string Payload { get; set; } = string.Empty;
    public int ResponseCode { get; set; }
    public bool Success { get; set; }
    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}