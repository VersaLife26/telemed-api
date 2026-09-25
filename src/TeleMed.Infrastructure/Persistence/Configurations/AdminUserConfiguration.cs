using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
{
    public void Configure(EntityTypeBuilder<AdminUser> builder)
    {
        builder.ToTable("admin_users", t => t.HasCheckConstraint("ck_admin_users_email_lower", "email = lower(email)"));
        builder.Property(a => a.Email).HasMaxLength(254);
        builder.Property(a => a.DisplayName).HasMaxLength(200);
        builder.HasIndex(a => a.Email).IsUnique().HasDatabaseName("ux_admin_users_email");
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(a => a.CreatedByAdminId).OnDelete(DeleteBehavior.Restrict);
    }
}
