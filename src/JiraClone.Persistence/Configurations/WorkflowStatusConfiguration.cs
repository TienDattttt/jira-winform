using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class WorkflowStatusConfiguration : IEntityTypeConfiguration<WorkflowStatus>
{
    public void Configure(EntityTypeBuilder<WorkflowStatus> builder)
    {
        builder.ToTable("WorkflowStatuses");
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(16).IsRequired();
        builder.HasIndex(x => new { x.WorkflowDefinitionId, x.Name }).IsUnique();
        builder.HasIndex(x => new { x.WorkflowDefinitionId, x.DisplayOrder }).IsUnique();
        builder.HasOne(x => x.WorkflowDefinition).WithMany(x => x.Statuses).HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}
