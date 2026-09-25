using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<User, Guid>(options)
{
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<Drug> Drugs => Set<Drug>();
    public DbSet<Icd10Code> Icd10Codes => Set<Icd10Code>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<CapturedMessage> CapturedMessages => Set<CapturedMessage>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DoctorApplication> DoctorApplications => Set<DoctorApplication>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorDocument> DoctorDocuments => Set<DoctorDocument>();
    public DbSet<WorkingHour> WorkingHours => Set<WorkingHour>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<SlotBlock> SlotBlocks => Set<SlotBlock>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<RescheduleRequest> RescheduleRequests => Set<RescheduleRequest>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();
    public DbSet<PromoRedemption> PromoRedemptions => Set<PromoRedemption>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AdminNotification> AdminNotifications => Set<AdminNotification>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<ConsultationMessage> ConsultationMessages => Set<ConsultationMessage>();
    public DbSet<ConsultationEvent> ConsultationEvents => Set<ConsultationEvent>();
    public DbSet<ClinicalNote> ClinicalNotes => Set<ClinicalNote>();
    public DbSet<ClinicalNoteDiagnosis> ClinicalNoteDiagnoses => Set<ClinicalNoteDiagnosis>();
    public DbSet<ClinicalNoteRevision> ClinicalNoteRevisions => Set<ClinicalNoteRevision>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<VaultFolder> VaultFolders => Set<VaultFolder>();
    public DbSet<VaultDocument> VaultDocuments => Set<VaultDocument>();
    public DbSet<RecordAccessLog> RecordAccessLogs => Set<RecordAccessLog>();
    public DbSet<PayoutBatch> PayoutBatches => Set<PayoutBatch>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<PayoutAdjustment> PayoutAdjustments => Set<PayoutAdjustment>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeComment> DisputeComments => Set<DisputeComment>();

    internal static void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .UseEnumCheckConstraints();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("btree_gist");

        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims")
            .HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_user_claims_users_user_id");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins")
            .HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_user_logins_users_user_id");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens")
            .HasOne<User>().WithMany().HasForeignKey(x => x.UserId).HasConstraintName("fk_user_tokens_users_user_id");

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        builder.UseSnakeCaseEnumStrings();
    }
}
