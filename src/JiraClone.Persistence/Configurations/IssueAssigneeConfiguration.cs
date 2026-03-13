using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class IssueAssigneeConfiguration : IEntityTypeConfiguration<IssueAssignee>
{
    public void Configure(EntityTypeBuilder<IssueAssignee> builder)
    {
        builder.ToTable("IssueAssignees");
        builder.HasKey(x => new { x.IssueId, x.UserId });
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.IssueId, x.AssignedAtUtc });
    }
}
