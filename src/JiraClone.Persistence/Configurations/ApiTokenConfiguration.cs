using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        builder.ToTable("ApiTokens");
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IsRevoked, x.ExpiresAtUtc });
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(120).IsRequired();
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasOne(x => x.User)
            .WithMany(x => x.ApiTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
