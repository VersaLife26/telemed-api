using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds", t =>
        {
            t.HasCheckConstraint("ck_refunds_amounts",
                "amount_cents > 0 AND commission_cents >= 0 AND provider_fee_cents >= 0 AND payout_cents >= 0 " +
                "AND commission_cents + provider_fee_cents + payout_cents = amount_cents");
            t.HasCheckConstraint("ck_refunds_percent", "percent BETWEEN 1 AND 100");
        });

        builder.Property(r => r.ProviderRefundId).HasMaxLength(100);
        builder.Property(r => r.FailureReason).HasMaxLength(500);
        builder.Property(r => r.Note).HasMaxLength(1000);
        builder.Property(r => r.RejectionReason).HasMaxLength(1000);
        builder.Property(r => r.Version).IsRowVersion();

        builder.HasOne<Payment>().WithMany().HasForeignKey(r => r.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Dispute>().WithMany().HasForeignKey(r => r.DisputeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(r => r.RequestedByAdminId)
            .HasConstraintName("fk_refunds_admin_users_requested_by_admin_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(r => r.ReviewedByAdminId)
            .HasConstraintName("fk_refunds_admin_users_reviewed_by_admin_id").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.PaymentId);
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
        builder.HasIndex(r => r.DisputeId);
        builder.HasIndex(r => r.Status).HasFilter("status = 'processing'");
    }
}
