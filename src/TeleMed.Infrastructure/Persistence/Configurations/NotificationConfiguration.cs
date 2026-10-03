using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.Property(n => n.TemplateKey).HasMaxLength(64);
        builder.Property(n => n.Recipient).HasMaxLength(256);
        builder.Property(n => n.Subject).HasMaxLength(500);
        builder.Property(n => n.LastError).HasMaxLength(500);
        builder.Property(n => n.DedupeKey).HasMaxLength(200);
        builder.Property(n => n.AttachmentFileName).HasMaxLength(255);

        builder.HasOne<User>().WithMany().HasForeignKey(n => n.UserId)
            .HasConstraintName("fk_notifications_users_user_id").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.DedupeKey).IsUnique();
        builder.HasIndex(n => n.NextAttemptAt).HasFilter("status IN ('pending', 'sending')");
        builder.HasIndex(n => n.SentAt).HasFilter("status = 'sent' AND body IS NOT NULL");
        builder.HasIndex(n => n.UserId);
    }
}
