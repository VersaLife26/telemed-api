using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Finance;

public enum LedgerEntryType
{
    Payment,
    Refund,
}

// Refund rows carry negative amounts, so summing a column gives the net movement.
public sealed record LedgerEntryDto(
    LedgerEntryType Type,
    Guid Id,
    Guid PaymentId,
    Guid AppointmentId,
    Guid DoctorId,
    DateTimeOffset OccurredAt,
    long AmountCents,
    long CommissionCents,
    long ProviderFeeCents,
    long PayoutCents,
    string Currency,
    PaymentProvider? Provider,
    string? Reference);

public sealed record LedgerTotalsDto(
    int PaymentCount,
    int RefundCount,
    long CapturedCents,
    long RefundedCents,
    long NetCents,
    long CommissionCents,
    long ProviderFeeCents,
    long PayoutCents);

public sealed record LedgerPageDto(IReadOnlyList<LedgerEntryDto> Items, int Page, int PageSize, long Total, LedgerTotalsDto Totals);

public sealed record LedgerQuery : PageQuery, IDateRange
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? DoctorId { get; init; }
}

public sealed record LedgerExportQuery : IDateRange
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? DoctorId { get; init; }
}

public sealed record LedgerFilter(DateTimeOffset From, DateTimeOffset To, Guid? DoctorId);

public sealed record CommissionDto(
    int CommissionBps,
    int ProviderFeeBps,
    long ProviderFeeFixedCents,
    string Currency,
    int PayoutHoldHours,
    IReadOnlyList<DoctorCommissionDto> DoctorRates);

public sealed record DoctorCommissionDto(
    Guid DoctorId,
    string DisplayName,
    string SlmcNumber,
    int CommissionBps);

public sealed record UpdateCommissionRequest(int CommissionBps);

public sealed record SetDoctorCommissionRequest(int? CommissionBps);

public sealed record AdminRefundDto(
    Guid Id,
    Guid PaymentId,
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    long AmountCents,
    long CommissionCents,
    long ProviderFeeCents,
    long PayoutCents,
    int Percent,
    string Currency,
    RefundReason Reason,
    RefundStatus Status,
    PaymentProvider? Provider,
    string? ProviderRefundId,
    string? FailureReason,
    string? Note,
    Guid? DisputeId,
    Guid? RequestedByAdminId,
    Guid? ReviewedByAdminId,
    DateTimeOffset? ReviewedAt,
    string? RejectionReason,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset CreatedAt);

public sealed record AdminRefundQuery : PageQuery
{
    public RefundStatus? Status { get; init; }
}

public sealed record CreateRefundRequest(long AmountCents, string Reason);

public sealed record RejectRefundRequest(string Reason);

public sealed record MarkRefundedRequest(string Reference);

public sealed record PromoCodeDto(
    Guid Id,
    string Code,
    string Description,
    PromoDiscountType DiscountType,
    int? PercentBps,
    long? AmountOffCents,
    long? MaxDiscountCents,
    long MinAmountCents,
    string Currency,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil,
    int? MaxRedemptions,
    int MaxPerUser,
    bool IsActive,
    int RedemptionCount,
    DateTimeOffset CreatedAt);

public sealed record PromoCodeQuery : PageQuery
{
    public bool? IsActive { get; init; }
}

public sealed record CreatePromoCodeRequest(
    string Code,
    string? Description,
    PromoDiscountType DiscountType,
    int? PercentBps,
    long? AmountOffCents,
    long? MaxDiscountCents,
    long MinAmountCents,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    int? MaxRedemptions,
    int MaxPerUser = 1);

public sealed record UpdatePromoCodeRequest(string? Description, DateTimeOffset? ValidUntil, int? MaxRedemptions, int? MaxPerUser, bool? IsActive);
