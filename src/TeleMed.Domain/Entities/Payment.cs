using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Entities;

public class Payment : Entity, IAuditable
{
    public Guid AppointmentId { get; init; }
    public Guid PatientId { get; init; }
    public Guid DoctorId { get; init; }
    public PaymentProvider? Provider { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string Currency { get; init; } = PlatformPolicy.Currency;

    public long GrossCents { get; init; }
    public long DiscountCents { get; set; }
    public long AmountCents { get; set; }
    public long? CapturedCents { get; set; }
    public long CommissionCents { get; set; }
    public long ProviderFeeCents { get; set; }
    public long PayoutCents { get; set; }
    public long RefundedCents { get; set; }
    public long RefundedCommissionCents { get; set; }
    public long RefundedProviderFeeCents { get; set; }
    public long RefundedPayoutCents { get; set; }
    public string? PromoCode { get; set; }

    public bool AuthorizeOnly { get; set; }
    public DateTimeOffset? IntentCreatedAt { get; set; }
    public string? ProviderPaymentId { get; set; }

    [AuditIgnore]
    public string? AuthorizationToken { get; set; }

    public DateTimeOffset? AuthorizedAt { get; set; }
    public DateTimeOffset? SucceededAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public DateTimeOffset? VoidedAt { get; set; }
    public string? FailureReason { get; set; }
    public int CaptureAttempts { get; set; }
    public DateTimeOffset? CaptureFailedAt { get; set; }
    // Set when the card hold would lapse before the appointment, so settlement captures it without waiting for the visit.
    public DateTimeOffset? CaptureRequestedAt { get; set; }

    public Guid? PayoutId { get; set; }

    public uint Version { get; init; }

    public PaymentSplit CurrentSplit() => new(CapturedCents ?? AmountCents, CommissionCents, ProviderFeeCents, PayoutCents);

    public void ApplySplit(PaymentSplit split) =>
        (CommissionCents, ProviderFeeCents, PayoutCents) = (split.CommissionCents, split.ProviderFeeCents, split.PayoutCents);
}
