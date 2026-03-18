using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class WebhookEndpointConfiguration : IEntityTypeConfiguration<WebhookEndpoint>
{
    public void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        builder.ToTable("WebhookEndpoints");
        builder.HasIndex(x => new { x.ProjectId, x.IsActive });
        builder.HasIndex(x => new { x.ProjectId, x.Name });
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Secret).HasMaxLength(256).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.HasOne(x => x.Project)
            .WithMany(x => x.WebhookEndpoints)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}