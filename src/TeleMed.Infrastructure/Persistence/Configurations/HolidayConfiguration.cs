using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("holidays");

        builder.Property(h => h.Reason).HasMaxLength(500);

        builder.HasOne<Doctor>().WithMany().HasForeignKey(h => h.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(h => h.CreatedByUserId)
            .HasConstraintName("fk_holidays_users_created_by_user_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(h => h.CreatedByAdminId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.DoctorId, h.Date }).IsUnique().AreNullsDistinct(false).HasDatabaseName("ux_holidays_doctor_date");
        builder.HasIndex(h => h.Date);
    }
}
