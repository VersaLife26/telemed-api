using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class OtpChallenge : Entity
{
    public required string Destination { get; init; }
    public MessageChannel Channel { get; init; }
    public required string CodeHash { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public int Attempts { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
