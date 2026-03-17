using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class ComponentConfiguration : IEntityTypeConfiguration<Component>
{
    public void Configure(EntityTypeBuilder<Component> builder)
    {
        builder.ToTable("Components");
        builder.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
        builder.HasIndex(x => x.LeadUserId);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasOne(x => x.LeadUser)
            .WithMany(x => x.LedComponents)
            .HasForeignKey(x => x.LeadUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
