using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class PromoRedemptionConfiguration : IEntityTypeConfiguration<PromoRedemption>
{
    public void Configure(EntityTypeBuilder<PromoRedemption> builder)
    {
        builder.ToTable("promo_redemptions", t => t.HasCheckConstraint("ck_promo_redemptions_discount_cents", "discount_cents > 0"));

        builder.Property(r => r.ReleaseReason).HasMaxLength(50);

        builder.HasOne<PromoCode>().WithMany().HasForeignKey(r => r.PromoCodeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId)
            .HasConstraintName("fk_promo_redemptions_users_user_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payment>().WithMany().HasForeignKey(r => r.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Appointment>().WithMany().HasForeignKey(r => r.AppointmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.PaymentId).IsUnique().HasFilter("status IN ('reserved', 'consumed')").HasDatabaseName("ux_promo_redemptions_payment_live");
        builder.HasIndex(r => new { r.PromoCodeId, r.UserId, r.Status });
        builder.HasIndex(r => r.ExpiresAt).HasFilter("status = 'reserved'");
    }
}
