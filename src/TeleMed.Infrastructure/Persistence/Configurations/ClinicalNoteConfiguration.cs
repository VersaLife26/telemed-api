using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class ClinicalNoteConfiguration : IEntityTypeConfiguration<ClinicalNote>
{
    public const string AppointmentIndex = "ux_clinical_notes_appointment_id";

    public void Configure(EntityTypeBuilder<ClinicalNote> builder)
    {
        var max = ClinicalNote.MaxSectionLength;
        builder.ToTable("clinical_notes", t =>
        {
            t.HasCheckConstraint("ck_clinical_notes_sections",
                $"char_length(subjective) <= {max} AND char_length(objective) <= {max} AND char_length(assessment) <= {max} AND char_length(plan) <= {max}");
            t.HasCheckConstraint("ck_clinical_notes_finalised",
                "(status = 'draft' AND finalised_at IS NULL AND revision = 0) OR (status = 'finalised' AND finalised_at IS NOT NULL AND revision >= 1)");
        });
        builder.Property(n => n.Version).IsRowVersion();
        builder.Ignore(n => n.HasContent);

        builder.HasOne<Appointment>().WithOne().HasForeignKey<ClinicalNote>(n => n.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(n => n.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(n => n.PatientId)
            .HasConstraintName("fk_clinical_notes_users_patient_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(n => n.Diagnoses).WithOne().HasForeignKey(d => d.NoteId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => n.AppointmentId).IsUnique().HasDatabaseName(AppointmentIndex);
        builder.HasIndex(n => new { n.DoctorId, n.UpdatedAt });
        builder.HasIndex(n => n.PatientId);
    }
}

internal sealed class ClinicalNoteDiagnosisConfiguration : IEntityTypeConfiguration<ClinicalNoteDiagnosis>
{
    // Created in the migration as a deferred exclusion constraint: EF cannot order a replaced primary's delete before the new
    // primary's insert under a filtered unique index, so the check waits for the commit.
    public const string OnePrimaryConstraint = "ex_clinical_note_diagnoses_one_primary";

    public void Configure(EntityTypeBuilder<ClinicalNoteDiagnosis> builder)
    {
        builder.ToTable("clinical_note_diagnoses");
        // Rows are added through the note's collection, so a preset id must still mean "new row".
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Code).HasMaxLength(10);
        builder.Property(d => d.Display).HasMaxLength(500);

        builder.HasOne<Icd10Code>().WithMany().HasForeignKey(d => d.Code).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.NoteId, d.Code }).IsUnique();
        builder.HasIndex(d => d.Code);
    }
}

internal sealed class ClinicalNoteRevisionConfiguration : IEntityTypeConfiguration<ClinicalNoteRevision>
{
    public void Configure(EntityTypeBuilder<ClinicalNoteRevision> builder)
    {
        builder.ToTable("clinical_note_revisions", t =>
        {
            t.HasCheckConstraint("ck_clinical_note_revisions_revision", "revision >= 1");
            t.HasCheckConstraint("ck_clinical_note_revisions_reason",
                "(change_type = 'finalise' AND amendment_reason IS NULL) OR (change_type = 'amend' AND char_length(btrim(amendment_reason)) > 0)");
        });
        builder.Property(r => r.Id).UseSerialColumn();
        builder.Property(r => r.Diagnoses).HasJsonbConversion();
        builder.Property(r => r.AmendmentReason).HasMaxLength(ClinicalNote.MaxAmendmentReasonLength);

        builder.HasOne<ClinicalNote>().WithMany().HasForeignKey(r => r.NoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.ChangedBy)
            .HasConstraintName("fk_clinical_note_revisions_users_changed_by").OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.NoteId, r.Revision }).IsUnique();
    }
}
