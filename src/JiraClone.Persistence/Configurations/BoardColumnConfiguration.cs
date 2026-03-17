using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class BoardColumnConfiguration : IEntityTypeConfiguration<BoardColumn>
{
    public void Configure(EntityTypeBuilder<BoardColumn> builder)
    {
        builder.ToTable("BoardColumns");
        builder.HasIndex(x => new { x.ProjectId, x.WorkflowStatusId }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.DisplayOrder }).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasOne(x => x.WorkflowStatus).WithMany(x => x.BoardColumns).HasForeignKey(x => x.WorkflowStatusId).OnDelete(DeleteBehavior.Restrict);
    }
}
