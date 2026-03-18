using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class ProjectIntegrationConfigConfiguration : IEntityTypeConfiguration<ProjectIntegrationConfig>
{
    public void Configure(EntityTypeBuilder<ProjectIntegrationConfig> builder)
    {
        builder.ToTable("ProjectIntegrationConfigs");
        builder.HasIndex(x => new { x.ProjectId, x.IntegrationName }).IsUnique();
        builder.Property(x => x.IntegrationName).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ConfigJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.IsEnabled).HasDefaultValue(true);
        builder.HasOne(x => x.Project)
            .WithMany(x => x.ProjectIntegrationConfigs)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
