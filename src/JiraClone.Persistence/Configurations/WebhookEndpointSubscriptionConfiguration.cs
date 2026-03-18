using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class WebhookEndpointSubscriptionConfiguration : IEntityTypeConfiguration<WebhookEndpointSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookEndpointSubscription> builder)
    {
        builder.ToTable("WebhookEndpointSubscriptions");
        builder.HasKey(x => new { x.WebhookEndpointId, x.EventType });
        builder.HasIndex(x => x.EventType);
        builder.HasOne(x => x.WebhookEndpoint)
            .WithMany(x => x.Subscriptions)
            .HasForeignKey(x => x.WebhookEndpointId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}