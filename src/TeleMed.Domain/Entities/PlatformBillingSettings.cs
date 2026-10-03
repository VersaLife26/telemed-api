using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

// One row. LkrPerUsd is how many rupees buy one US dollar when an international patient is charged.
public class PlatformBillingSettings : Entity, IAuditable
{
    public decimal? LkrPerUsd { get; set; }
}
