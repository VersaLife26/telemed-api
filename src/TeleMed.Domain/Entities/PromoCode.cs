using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Entities;

public class PromoCode : Entity, IAuditable
{
    public required string Code { get; init; }
    public string Description { get; set; } = "";
    public PromoDiscountType DiscountType { get; init; }
    public int? PercentBps { get; init; }
    public long? AmountOffCents { get; init; }
    public long? MaxDiscountCents { get; init; }
    public long MinAmountCents { get; init; }
    public string Currency { get; init; } = PlatformPolicy.Currency;
    public DateTimeOffset ValidFrom { get; init; }
    public DateTimeOffset? ValidUntil { get; set; }
    public int? MaxRedemptions { get; set; }
    public int MaxPerUser { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public uint Version { get; init; }
}
