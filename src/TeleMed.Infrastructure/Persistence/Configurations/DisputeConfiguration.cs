using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.ToTable("disputes");
        builder.Property(d => d.Subject).HasMaxLength(200);
        builder.Property(d => d.Description).HasMaxLength(4000);
        builder.Property(d => d.Resolution).HasMaxLength(4000);
        builder.Property(d => d.Version).IsRowVersion();

        builder.HasOne<Appointment>().WithMany().HasForeignKey(d => d.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.PatientId)
            .HasConstraintName("fk_disputes_users_patient_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(d => d.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(d => d.OpenedByAdminId)
            .HasConstraintName("fk_disputes_admin_users_opened_by_admin_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(d => d.AssignedAdminId)
            .HasConstraintName("fk_disputes_admin_users_assigned_admin_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(d => d.ResolvedByAdminId)
            .HasConstraintName("fk_disputes_admin_users_resolved_by_admin_id").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.Status, d.CreatedAt });
        builder.HasIndex(d => d.AppointmentId);
    }
}

internal sealed class DisputeCommentConfiguration : IEntityTypeConfiguration<DisputeComment>
{
    public void Configure(EntityTypeBuilder<DisputeComment> builder)
    {
        builder.ToTable("dispute_comments");
        builder.Property(c => c.Body).HasMaxLength(4000);
        builder.HasOne<Dispute>().WithMany().HasForeignKey(c => c.DisputeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AdminUser>().WithMany().HasForeignKey(c => c.AuthorAdminId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => new { c.DisputeId, c.CreatedAt });
    }
}
