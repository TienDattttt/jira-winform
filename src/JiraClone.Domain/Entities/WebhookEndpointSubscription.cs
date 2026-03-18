using JiraClone.Domain.Enums;

namespace JiraClone.Domain.Entities;

public class WebhookEndpointSubscription
{
    public int WebhookEndpointId { get; set; }
    public WebhookEndpoint WebhookEndpoint { get; set; } = null!;
    public WebhookEventType EventType { get; set; }
}