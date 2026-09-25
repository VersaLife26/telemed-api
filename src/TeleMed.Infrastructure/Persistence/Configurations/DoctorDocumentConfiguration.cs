using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class DoctorDocumentConfiguration : IEntityTypeConfiguration<DoctorDocument>
{
    public void Configure(EntityTypeBuilder<DoctorDocument> builder)
    {
        builder.ToTable("doctor_documents", t =>
            t.HasCheckConstraint("ck_doctor_documents_owner", "application_id IS NOT NULL OR doctor_id IS NOT NULL"));

        builder.Property(d => d.StorageKey).HasMaxLength(500);
        builder.Property(d => d.FileName).HasMaxLength(255);
        builder.Property(d => d.ContentType).HasMaxLength(100);
        builder.Property(d => d.Sha256).HasMaxLength(64).IsFixedLength();

        builder.HasOne<DoctorApplication>().WithMany().HasForeignKey(d => d.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(d => d.DoctorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.DoctorId);
        builder.HasIndex(d => new { d.DoctorId, d.Type })
            .IsUnique()
            .HasFilter("deleted_at IS NULL AND type IN ('signature', 'seal')")
            .HasDatabaseName("ux_doctor_documents_doctor_stamp_live");
        builder.HasIndex(d => new { d.ApplicationId, d.Type })
            .IsUnique()
            .HasFilter("deleted_at IS NULL")
            .HasDatabaseName("ux_doctor_documents_application_type_live");
    }
}
