using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> builder)
    {
        builder.ToTable("payment_webhook_events");

        builder.Property(e => e.EventId).HasMaxLength(200);
        builder.Property(e => e.Outcome).HasMaxLength(50);
        builder.Property(e => e.Payload).HasColumnType("jsonb");

        builder.HasOne<Payment>().WithMany().HasForeignKey(e => e.PaymentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.Provider, e.EventId }).IsUnique().HasDatabaseName("ux_payment_webhook_events_provider_event_id");
    }
}
