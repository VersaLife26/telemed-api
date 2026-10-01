using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.Email, t.CreatedAt });
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId)
            .HasConstraintName("fk_password_reset_tokens_users_user_id").OnDelete(DeleteBehavior.Cascade);
    }
}
