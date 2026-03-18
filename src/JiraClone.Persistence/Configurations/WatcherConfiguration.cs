using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using JiraClone.Domain.Entities;

namespace JiraClone.Persistence.Configurations;

public class WatcherConfiguration : IEntityTypeConfiguration<Watcher>
{
    public void Configure(EntityTypeBuilder<Watcher> builder)
    {
        builder.ToTable("Watchers");
        builder.HasKey(x => new { x.IssueId, x.UserId });
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.IssueId, x.WatchedAtUtc });
        builder.HasOne(x => x.Issue)
            .WithMany(x => x.Watchers)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User)
            .WithMany(x => x.WatchedIssues)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
