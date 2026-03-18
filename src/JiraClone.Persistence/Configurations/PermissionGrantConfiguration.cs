using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class PermissionGrantConfiguration : IEntityTypeConfiguration<PermissionGrant>
{
    public void Configure(EntityTypeBuilder<PermissionGrant> builder)
    {
        builder.ToTable("PermissionGrants");
        builder.HasKey(x => new { x.PermissionSchemeId, x.Permission, x.ProjectRole });
        builder.HasOne(x => x.PermissionScheme)
            .WithMany(x => x.Grants)
            .HasForeignKey(x => x.PermissionSchemeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
