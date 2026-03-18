using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("WebhookDeliveries");
        builder.HasIndex(x => new { x.WebhookEndpointId, x.AttemptedAtUtc });
        builder.Property(x => x.Payload).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.ResponseCode).HasDefaultValue(0);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.HasOne(x => x.WebhookEndpoint)
            .WithMany(x => x.Deliveries)
            .HasForeignKey(x => x.WebhookEndpointId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}