using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class ConsultationMessageConfiguration : IEntityTypeConfiguration<ConsultationMessage>
{
    public void Configure(EntityTypeBuilder<ConsultationMessage> builder)
    {
        builder.ToTable("consultation_messages", t =>
            t.HasCheckConstraint("ck_consultation_messages_body", $"char_length(body) BETWEEN 1 AND {ConsultationMessage.MaxBodyLength}"));
        builder.Property(m => m.Body).HasMaxLength(ConsultationMessage.MaxBodyLength);

        builder.HasOne<Consultation>().WithMany().HasForeignKey(m => m.ConsultationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.SenderUserId)
            .HasConstraintName("fk_consultation_messages_users_sender_user_id").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.ConsultationId, m.CreatedAt });
    }
}
