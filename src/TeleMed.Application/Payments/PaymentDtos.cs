using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Payments;

public sealed record OrderSummaryDto(
    Guid PaymentId,
    Guid AppointmentId,
    PaymentStatus Status,
    long GrossCents,
    long DiscountCents,
    long AmountCents,
    string Currency,
    string? PromoCode,
    DateTimeOffset? PromoExpiresAt,
    PaymentProvider? Provider,
    bool IntentCreated,
    DateTimeOffset? PaymentDueAt,
    IReadOnlyList<PaymentProvider> AvailableProviders);

public sealed record ApplyPromoRequest(string Code);

public sealed record CreateIntentRequest(PaymentProvider Provider);

public sealed record PayHereCheckoutDto(string ActionUrl, IReadOnlyDictionary<string, string> Fields);

public sealed record PaymentIntentDto(
    Guid PaymentId,
    PaymentProvider Provider,
    PaymentStatus Status,
    bool AuthorizeOnly,
    long AmountCents,
    string Currency,
    string Reference,
    PayHereCheckoutDto? Checkout);

public enum MockOutcome
{
    Succeed,
    Fail,
}

public sealed record MockCompleteRequest(MockOutcome Outcome);

public sealed record RefundDto(Guid Id, long AmountCents, int Percent, RefundReason Reason, RefundStatus Status, DateTimeOffset? ProcessedAt, DateTimeOffset CreatedAt);

public sealed record PaymentDto(
    Guid Id,
    Guid AppointmentId,
    Guid DoctorId,
    PaymentStatus Status,
    PaymentProvider? Provider,
    long GrossCents,
    long DiscountCents,
    long AmountCents,
    long? CapturedCents,
    long RefundedCents,
    string Currency,
    string? PromoCode,
    bool AuthorizeOnly,
    DateTimeOffset? AuthorizedAt,
    DateTimeOffset? SucceededAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<RefundDto> Refunds);

public sealed record PaymentQuery : PageQuery;

public sealed record SettlementCandidate(
    Guid PaymentId,
    PaymentProvider Provider,
    string? AuthorizationToken,
    long AmountCents,
    string Currency,
    AppointmentStatus AppointmentStatus,
    int? RefundPercent);

public sealed record PendingRefund(
    Guid RefundId,
    Guid PaymentId,
    PaymentProvider Provider,
    string? ProviderPaymentId,
    long AmountCents,
    long CapturedCents,
    string Currency);

public sealed record ExpiredReservation(Guid RedemptionId, Guid PaymentId);
