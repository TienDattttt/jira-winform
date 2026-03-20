using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class IssueLabelConfiguration : IEntityTypeConfiguration<IssueLabel>
{
    public void Configure(EntityTypeBuilder<IssueLabel> builder)
    {
        builder.ToTable("IssueLabels");
        builder.HasKey(x => new { x.IssueId, x.LabelId });
        builder.HasIndex(x => x.LabelId);

        builder.HasOne(x => x.Label)
            .WithMany(x => x.IssueLabels)
            .HasForeignKey(x => x.LabelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Issue)
            .WithMany(x => x.IssueLabels)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}