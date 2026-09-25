using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Payouts;

public sealed record PayoutBatchDto(
    Guid Id,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    PayoutBatchStatus Status,
    int PayoutCount,
    long TotalCents,
    Guid? CreatedByAdminId,
    DateTimeOffset CreatedAt);

public sealed record PayoutDto(
    Guid Id,
    Guid BatchId,
    Guid DoctorId,
    string DoctorName,
    DateOnly Period,
    long AmountCents,
    int PaymentCount,
    string Currency,
    PayoutStatus Status,
    string? TransferReference,
    string? FailureReason,
    DateTimeOffset? PaidAt,
    DateTimeOffset? FailedAt,
    Guid? MarkedByAdminId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PayoutAdjustmentDto> Adjustments);

// A clawback deducted from this payout: the doctor's share of a refund that settled after its payment had been paid out.
public sealed record PayoutAdjustmentDto(Guid RefundId, Guid PaymentId, long AmountCents);

public sealed record PayoutBatchDetailDto(PayoutBatchDto Batch, IReadOnlyList<PayoutDto> Payouts);

public sealed record PayoutRunDto(DateOnly Period, bool Created, PayoutBatchDetailDto? Batch);

public sealed record DoctorPayoutDto(
    Guid Id,
    DateOnly Period,
    long AmountCents,
    int PaymentCount,
    string Currency,
    PayoutStatus Status,
    string? TransferReference,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PayoutAdjustmentDto> Adjustments);

public sealed record RunPayoutsRequest(DateOnly? Date);

public sealed record MarkPayoutPaidRequest(string TransferReference);

public sealed record MarkPayoutFailedRequest(string Reason);

public sealed record PayoutBatchQuery : PageQuery
{
    public PayoutBatchStatus? Status { get; init; }
}

public sealed record DoctorPayoutQuery : PageQuery;
