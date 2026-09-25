using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class Refund : Entity, IAuditable
{
    public Guid PaymentId { get; init; }
    public long AmountCents { get; init; }
    public long CommissionCents { get; init; }
    public long ProviderFeeCents { get; init; }
    public long PayoutCents { get; init; }
    public int Percent { get; init; }
    public RefundReason Reason { get; init; }
    public RefundStatus Status { get; set; } = RefundStatus.Processing;
    public string? ProviderRefundId { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public Guid? DisputeId { get; init; }
    public string? Note { get; init; }
    public Guid? RequestedByAdminId { get; init; }
    public Guid? ReviewedByAdminId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public uint Version { get; init; }
}
