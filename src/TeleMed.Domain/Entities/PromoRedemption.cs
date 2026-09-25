using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class PromoRedemption : Entity
{
    public Guid PromoCodeId { get; init; }
    public Guid UserId { get; init; }
    public Guid PaymentId { get; init; }
    public Guid AppointmentId { get; init; }
    public long DiscountCents { get; init; }
    public PromoRedemptionStatus Status { get; set; } = PromoRedemptionStatus.Reserved;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string? ReleaseReason { get; set; }
}
