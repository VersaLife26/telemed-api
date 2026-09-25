using TeleMed.Domain.Common;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Entities;

// Claws back the doctor's share of a refund that settled after its payment had already gone into a payout.
public class PayoutAdjustment : Entity, IAuditable
{
    public Guid DoctorId { get; init; }
    public Guid RefundId { get; init; }
    public Guid PaymentId { get; init; }
    public long AmountCents { get; init; }
    public string Currency { get; init; } = PlatformPolicy.Currency;
    public Guid? AppliedPayoutId { get; set; }
    public uint Version { get; init; }
}
