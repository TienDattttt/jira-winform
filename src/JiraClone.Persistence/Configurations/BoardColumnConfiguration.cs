using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class BoardColumnConfiguration : IEntityTypeConfiguration<BoardColumn>
{
    public void Configure(EntityTypeBuilder<BoardColumn> builder)
    {
        builder.ToTable("BoardColumns");
        builder.HasIndex(x => new { x.ProjectId, x.StatusCode }).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
    }
}
