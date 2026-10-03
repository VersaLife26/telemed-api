using TeleMed.Domain.Common;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Entities;

public class PlatformCommissionPolicy : Entity, IAuditable
{
    public static readonly Guid SingletonId = Guid.Parse("c0111551-0001-7000-8000-000000000001");

    public int DefaultCommissionBps { get; set; } = PlatformPolicy.CommissionBps;
}
