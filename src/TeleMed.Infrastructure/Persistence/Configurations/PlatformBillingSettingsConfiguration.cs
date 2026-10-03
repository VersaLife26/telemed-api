using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class PlatformBillingSettingsConfiguration : IEntityTypeConfiguration<PlatformBillingSettings>
{
    public static readonly Guid SingletonId = Guid.Parse("0199a000-0000-7000-8000-000000000001");
    private static readonly DateTimeOffset SeededAt = new(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<PlatformBillingSettings> builder)
    {
        builder.ToTable("platform_billing_settings", t =>
        {
            t.HasCheckConstraint("ck_platform_billing_settings_lkr_per_usd",
                "lkr_per_usd IS NULL OR (lkr_per_usd >= "
                + ForeignPricing.MinLkrPerUsd.ToString(CultureInfo.InvariantCulture)
                + " AND lkr_per_usd <= "
                + ForeignPricing.MaxLkrPerUsd.ToString(CultureInfo.InvariantCulture)
                + ")");
        });

        builder.Property(s => s.LkrPerUsd).HasPrecision(12, 4);
        builder.HasData(new PlatformBillingSettings
        {
            Id = SingletonId,
            LkrPerUsd = null,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt,
        });
    }
}
