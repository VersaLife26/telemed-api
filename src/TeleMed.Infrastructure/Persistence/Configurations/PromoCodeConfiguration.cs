using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        builder.ToTable("promo_codes", t =>
        {
            t.HasCheckConstraint("ck_promo_codes_code_upper", "code = upper(code)");
            t.HasCheckConstraint("ck_promo_codes_discount",
                "(discount_type = 'percent' AND percent_bps BETWEEN 1 AND 10000 AND amount_off_cents IS NULL) " +
                "OR (discount_type = 'fixed' AND amount_off_cents > 0 AND percent_bps IS NULL AND max_discount_cents IS NULL)");
            t.HasCheckConstraint("ck_promo_codes_limits",
                "min_amount_cents >= 0 AND (max_discount_cents IS NULL OR max_discount_cents > 0) " +
                "AND (max_redemptions IS NULL OR max_redemptions > 0) AND max_per_user > 0");
            t.HasCheckConstraint("ck_promo_codes_validity", "valid_until IS NULL OR valid_until > valid_from");
        });

        builder.Property(p => p.Code).HasMaxLength(50);
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(p => p.Version).IsRowVersion();

        builder.HasIndex(p => p.Code).IsUnique().HasDatabaseName("ux_promo_codes_code");
    }
}
