using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Entities;

public class Payout : Entity, IAuditable
{
    public Guid BatchId { get; init; }
    public Guid DoctorId { get; init; }
    public DateOnly Period { get; init; }
    public long AmountCents { get; init; }
    public int PaymentCount { get; init; }
    public string Currency { get; init; } = PlatformPolicy.Currency;
    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;
    public string? TransferReference { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public Guid? MarkedByAdminId { get; set; }
    public uint Version { get; init; }
}
