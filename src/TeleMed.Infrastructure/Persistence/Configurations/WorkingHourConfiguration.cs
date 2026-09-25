using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class WorkingHourConfiguration : IEntityTypeConfiguration<WorkingHour>
{
    public void Configure(EntityTypeBuilder<WorkingHour> builder)
    {
        builder.ToTable("working_hours", t =>
            t.HasCheckConstraint("ck_working_hours_minutes", "start_minute >= 0 AND end_minute <= 1440 AND end_minute > start_minute"));

        builder.Property(h => h.DayOfWeek).HasConversion(d => (int)d, v => (DayOfWeek)v);

        builder.HasOne<Doctor>().WithMany().HasForeignKey(h => h.DoctorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.DoctorId, h.DayOfWeek, h.StartMinute }).IsUnique().HasDatabaseName("ux_working_hours_doctor_day_start");
    }
}
