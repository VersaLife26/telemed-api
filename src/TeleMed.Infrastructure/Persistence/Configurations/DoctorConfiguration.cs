using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Infrastructure.Persistence.Configurations;

internal sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public const string SearchVector = "SearchVector";
    public const string LanguagesCheck = "cardinality(languages) > 0 AND languages <@ ARRAY['en', 'si', 'ta', 'other']::text[]";

    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("doctors", t =>
        {
            t.HasCheckConstraint("ck_doctors_fee_cents", $"fee_cents BETWEEN {PlatformPolicy.MinFeeCents} AND {PlatformPolicy.MaxFeeCents}");
            t.HasCheckConstraint("ck_doctors_languages", LanguagesCheck);
            t.HasCheckConstraint("ck_doctors_experience_years", "experience_years >= 0");
            t.HasCheckConstraint("ck_doctors_schedule", "slot_duration_minutes > 0 AND buffer_minutes >= 0 AND max_per_day > 0 AND advance_days > 0");
        });

        builder.Property(d => d.SlmcNumber).HasMaxLength(20);
        builder.Property(d => d.SpecialtyCode).HasMaxLength(50);
        builder.Property(d => d.DisplayName).HasMaxLength(200);
        builder.Property(d => d.Bio).HasMaxLength(2000);
        builder.PrimitiveCollection(d => d.Languages).ElementType(e => e.HasConversion<SnakeCaseEnumConverter<ConsultationLanguage>>());
        builder.Property(d => d.LanguageOther).HasMaxLength(100);
        builder.Property(d => d.Qualifications).HasJsonbConversion();
        builder.Property(d => d.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(d => d.BankName).HasMaxLength(100);
        builder.Property(d => d.BankBranch).HasMaxLength(100);
        builder.Property(d => d.SuspendedReason).HasMaxLength(1000);
        builder.Property(d => d.SlotDurationMinutes).HasDefaultValue(PlatformPolicy.DefaultSlotDurationMinutes);
        builder.Property(d => d.BufferMinutes).HasDefaultValue(PlatformPolicy.DefaultBufferMinutes);
        builder.Property(d => d.MaxPerDay).HasDefaultValue(PlatformPolicy.DefaultMaxPerDay);
        builder.Property(d => d.AdvanceDays).HasDefaultValue(PlatformPolicy.DefaultAdvanceDays);
        builder.Property(d => d.TimeZone).HasMaxLength(64).HasDefaultValue(PlatformPolicy.TimeZoneId);
        builder.Property(d => d.Version).IsRowVersion();

        builder.Property<NpgsqlTsVector>(SearchVector)
            .HasComputedColumnSql(
                "setweight(to_tsvector('english', display_name), 'A') || " +
                "setweight(to_tsvector('english', replace(specialty_code, '_', ' ')), 'B') || " +
                "setweight(to_tsvector('english', bio), 'C')",
                stored: true);
        builder.HasIndex(SearchVector).HasMethod("GIN");
        builder.HasIndex(d => d.Languages).HasMethod("GIN");

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.UserId)
            .HasConstraintName("fk_doctors_users_user_id").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Specialty>().WithMany().HasForeignKey(d => d.SpecialtyCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorApplication>().WithMany().HasForeignKey(d => d.ApplicationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.UserId).IsUnique().HasDatabaseName("ux_doctors_user_id");
        builder.HasIndex(d => d.SlmcNumber).IsUnique().HasDatabaseName("ux_doctors_slmc_number");
        builder.HasIndex(d => d.ApplicationId).IsUnique().HasDatabaseName("ux_doctors_application_id");
        builder.HasIndex(d => new { d.Status, d.SpecialtyCode });
    }
}
