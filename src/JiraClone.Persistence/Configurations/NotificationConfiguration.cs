using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasIndex(x => new { x.RecipientUserId, x.IsRead, x.CreatedAtUtc });
        builder.HasIndex(x => x.IssueId);
        builder.HasIndex(x => x.ProjectId);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.IsRead).HasDefaultValue(false);
        builder.HasOne(x => x.RecipientUser)
            .WithMany(x => x.Notifications)
            .HasForeignKey(x => x.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Issue)
            .WithMany(x => x.Notifications)
            .HasForeignKey(x => x.IssueId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Project)
            .WithMany(x => x.Notifications)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}