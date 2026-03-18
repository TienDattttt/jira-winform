using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class SavedFilterConfiguration : IEntityTypeConfiguration<SavedFilter>
{
    public void Configure(EntityTypeBuilder<SavedFilter> builder)
    {
        builder.ToTable("SavedFilters");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.QueryText).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => new { x.ProjectId, x.UserId, x.Name }).IsUnique();

        builder.HasOne(x => x.Project)
            .WithMany(x => x.SavedFilters)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.SavedFilters)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
