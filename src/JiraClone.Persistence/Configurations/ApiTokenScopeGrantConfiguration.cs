using JiraClone.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JiraClone.Persistence.Configurations;

public class ApiTokenScopeGrantConfiguration : IEntityTypeConfiguration<ApiTokenScopeGrant>
{
    public void Configure(EntityTypeBuilder<ApiTokenScopeGrant> builder)
    {
        builder.ToTable("ApiTokenScopes");
        builder.HasKey(x => new { x.ApiTokenId, x.Scope });
        builder.HasIndex(x => x.Scope);
        builder.HasOne(x => x.ApiToken)
            .WithMany(x => x.ScopeGrants)
            .HasForeignKey(x => x.ApiTokenId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
