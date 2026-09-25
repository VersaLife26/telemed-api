using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ux_users_normalized_user_name");

        builder.HasIndex(u => u.NormalizedEmail)
            .HasDatabaseName("ux_users_normalized_email")
            .IsUnique()
            .HasFilter("normalized_email IS NOT NULL AND status <> 'deleted'");

        builder.HasIndex(u => u.PhoneNumber)
            .HasDatabaseName("ux_users_phone_number")
            .IsUnique()
            .HasFilter("phone_number IS NOT NULL AND status <> 'deleted'");

        builder.HasIndex(u => u.ErasureDueAt)
            .HasFilter("erasure_due_at IS NOT NULL AND anonymized_at IS NULL");
    }
}
