using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class PermissionSchemeConfiguration : IEntityTypeConfiguration<PermissionScheme>
{
    public void Configure(EntityTypeBuilder<PermissionScheme> builder)
    {
        builder.ToTable("PermissionSchemes");
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.HasIndex(x => x.ProjectId).IsUnique();
        builder.HasOne(x => x.Project)
            .WithOne(x => x.PermissionScheme)
            .HasForeignKey<PermissionScheme>(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
