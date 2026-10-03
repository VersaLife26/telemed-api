namespace TeleMed.Domain.Rules;

public static class PlatformPolicy
{
    public const string TimeZoneId = "Asia/Colombo";
    public const string Currency = "LKR";

    public const int DefaultSlotDurationMinutes = 15;
    public const int DefaultBufferMinutes = 5;
    public const int DefaultMaxPerDay = 24;
    public const int DefaultAdvanceDays = 30;

    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan PromoReservationTtl = TimeSpan.FromMinutes(30);
    // PayHere card holds lapse after 7 days; anything starting later is charged at once.
    public static readonly TimeSpan MaxCardHoldLead = TimeSpan.FromDays(6);
    public const long MinChargeableCents = 100;
    public const string PaymentTimeoutReason = "payment_timeout";
    public const string DoctorOnLeaveReason = "doctor_on_leave";
    public const string SlotBlockedReason = "slot_blocked";
    public const string RescheduleDeclinedReason = "reschedule_declined";
    public const string RescheduleExpiredReason = "reschedule_expired";
    public const string PatientSuspendedReason = "patient_suspended";
    public const int MaxCaptureAttempts = 10;

    public static readonly TimeSpan FreeCancellationWindow = TimeSpan.FromHours(2);
    public const int LateCancellationRefundPercent = 50;
    public const int FullRefundPercent = 100;

    public const int CommissionBps = 2_000;
    public const int ProviderFeeBps = 300;
    // Leave room for the provider fee so payout never goes negative at the maximum rate.
    public const int MaxCommissionBps = CommissionCalculator.BasisPoints - ProviderFeeBps;
    public const long ProviderFeeFixedCents = 0;
    public const long MinFeeCents = 50_000;
    public const long MaxFeeCents = 5_000_000;

    public static readonly TimeSpan OtpTtl = TimeSpan.FromMinutes(5);
    public const int OtpMaxAttempts = 5;
    public const int OtpSendLimit = 3;
    public static readonly TimeSpan OtpSendWindow = TimeSpan.FromHours(1);

    public static readonly TimeSpan PasswordResetTtl = TimeSpan.FromMinutes(30);
    public const int PasswordResetSendLimit = 3;
    public static readonly TimeSpan PasswordResetSendWindow = TimeSpan.FromHours(1);

    public static readonly TimeSpan RefreshReuseGrace = TimeSpan.FromSeconds(30);
    public const long ProfilePhotoMaxBytes = 5 * 1024 * 1024;
    public const long DoctorDocumentMaxBytes = 5 * 1024 * 1024;
    public const long WaitingRoomVideoMaxBytes = 25 * 1024 * 1024;

    public static readonly TimeSpan ErasureGracePeriod = TimeSpan.FromDays(30);
    public const long VaultMaxDocumentBytes = 10 * 1024 * 1024;
    public static readonly TimeSpan TreatingAccessWindow = TimeSpan.FromDays(30) + TimeSpan.FromMinutes(5);

    public static readonly TimeSpan AutoCompleteAfter = TimeSpan.FromHours(12);
    public static readonly TimeSpan StaleConsultationAfter = TimeSpan.FromHours(2);

    public static readonly TimeSpan JoinOpensBefore = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DoctorJoinGraceAfterEnd = TimeSpan.FromHours(2);
    public static readonly TimeSpan RunningLateLookahead = TimeSpan.FromMinutes(30);
    public const int WaitEstimateSampleSize = 10;
    public const int PoorQualityStreak = 3;
    public const string DoctorNoShowReason = "doctor_no_show";
    public const string PatientNoShowReason = "patient_no_show";
    public const string ConsultationStaleReason = "stale";
    public const string ConsultationEndedReason = "ended";

    public static readonly TimeSpan PayoutHold = TimeSpan.FromHours(24);
    public const int DailyPayoutsLocalHour = 2;

    public static readonly IReadOnlyList<TimeSpan> ReminderLeadTimes = [TimeSpan.FromHours(24), TimeSpan.FromHours(1)];
    public static readonly IReadOnlyList<TimeSpan> NotificationBackoff =
    [
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
    ];

    public static readonly TimeSpan HousekeepingRetention = TimeSpan.FromDays(7);
    public static readonly TimeSpan NotificationBodyRetention = TimeSpan.FromDays(90);
}
