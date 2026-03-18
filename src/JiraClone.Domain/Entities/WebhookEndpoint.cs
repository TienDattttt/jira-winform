using JiraClone.Domain.Common;

namespace JiraClone.Domain.Entities;

public class WebhookEndpoint : AggregateRoot
{
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<WebhookEndpointSubscription> Subscriptions { get; set; } = new List<WebhookEndpointSubscription>();
    public ICollection<WebhookDelivery> Deliveries { get; set; } = new List<WebhookDelivery>();
}