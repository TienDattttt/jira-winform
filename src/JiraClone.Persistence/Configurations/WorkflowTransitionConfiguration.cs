using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransitions");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => new { x.WorkflowDefinitionId, x.FromStatusId, x.ToStatusId }).IsUnique();
        builder.HasOne(x => x.WorkflowDefinition).WithMany(x => x.Transitions).HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.FromStatus).WithMany(x => x.OutgoingTransitions).HasForeignKey(x => x.FromStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToStatus).WithMany(x => x.IncomingTransitions).HasForeignKey(x => x.ToStatusId).OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(x => x.AllowedRoles)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "WorkflowTransitionRole",
                left => left.HasOne<Role>().WithMany().HasForeignKey("RoleId").OnDelete(DeleteBehavior.Cascade),
                right => right.HasOne<WorkflowTransition>().WithMany().HasForeignKey("WorkflowTransitionId").OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("WorkflowTransitionRoles");
                    join.HasKey("WorkflowTransitionId", "RoleId");
                });
    }
}
