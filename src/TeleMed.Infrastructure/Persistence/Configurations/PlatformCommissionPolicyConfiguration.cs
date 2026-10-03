using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class PlatformCommissionPolicyConfiguration : IEntityTypeConfiguration<PlatformCommissionPolicy>
{
    public void Configure(EntityTypeBuilder<PlatformCommissionPolicy> builder)
    {
        builder.ToTable("platform_commission_policies", t =>
        {
            t.HasCheckConstraint(
                "ck_platform_commission_policies_bps",
                $"default_commission_bps BETWEEN 0 AND {PlatformPolicy.MaxCommissionBps}");
        });
    }
}
