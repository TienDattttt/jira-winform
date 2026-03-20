using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class IssueComponentConfiguration : IEntityTypeConfiguration<IssueComponent>
{
    public void Configure(EntityTypeBuilder<IssueComponent> builder)
    {
        builder.ToTable("IssueComponents");
        builder.HasKey(x => new { x.IssueId, x.ComponentId });
        builder.HasIndex(x => x.ComponentId);

        builder.HasOne(x => x.Component)
            .WithMany(x => x.IssueComponents)
            .HasForeignKey(x => x.ComponentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Issue)
            .WithMany(x => x.IssueComponents)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}