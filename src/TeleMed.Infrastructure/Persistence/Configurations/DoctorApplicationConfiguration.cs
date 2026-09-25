using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class DoctorApplicationConfiguration : IEntityTypeConfiguration<DoctorApplication>
{
    public const string OpenFilter = "status IN ('pending', 'under_review')";

    public void Configure(EntityTypeBuilder<DoctorApplication> builder)
    {
        builder.ToTable("doctor_applications", t =>
        {
            t.HasCheckConstraint("ck_doctor_applications_email_lower", "email = lower(email)");
            t.HasCheckConstraint("ck_doctor_applications_fee_cents", $"fee_cents BETWEEN {PlatformPolicy.MinFeeCents} AND {PlatformPolicy.MaxFeeCents}");
            t.HasCheckConstraint("ck_doctor_applications_languages", DoctorConfiguration.LanguagesCheck);
            t.HasCheckConstraint("ck_doctor_applications_experience_years", "experience_years >= 0");
        });

        builder.Property(a => a.Phone).HasMaxLength(20);
        builder.Property(a => a.Email).HasMaxLength(254);
        builder.Property(a => a.FirstName).HasMaxLength(100);
        builder.Property(a => a.LastName).HasMaxLength(100);
        builder.Property(a => a.DisplayName).HasMaxLength(200);
        builder.Property(a => a.SlmcNumber).HasMaxLength(20);
        builder.Property(a => a.SpecialtyCode).HasMaxLength(50);
        builder.PrimitiveCollection(a => a.Languages).ElementType(e => e.HasConversion<SnakeCaseEnumConverter<ConsultationLanguage>>());
        builder.Property(a => a.LanguageOther).HasMaxLength(100);
        builder.Property(a => a.Bio).HasMaxLength(2000);
        builder.Property(a => a.MedicalSchool).HasMaxLength(200);
        builder.Property(a => a.QualificationsText).HasMaxLength(2000);
        builder.Property(a => a.AvailabilityNotes).HasMaxLength(2000);
        builder.Property(a => a.BankName).HasMaxLength(100);
        builder.Property(a => a.BankBranch).HasMaxLength(100);
        builder.Property(a => a.UploadTokenHash).HasMaxLength(64);
        builder.Property(a => a.RejectionReason).HasMaxLength(1000);
        builder.Property(a => a.Checklist).HasJsonbConversion();
        builder.Property(a => a.Version).IsRowVersion();
        builder.Ignore(a => a.IsOpen);

        builder.HasOne<Specialty>().WithMany().HasForeignKey(a => a.SpecialtyCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.Phone).IsUnique().HasFilter(OpenFilter).HasDatabaseName("ux_doctor_applications_phone_open");
        builder.HasIndex(a => a.SlmcNumber).IsUnique().HasFilter(OpenFilter).HasDatabaseName("ux_doctor_applications_slmc_number_open");
        builder.HasIndex(a => new { a.Status, a.CreatedAt });
    }
}
