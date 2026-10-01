using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

public class PasswordResetToken : Entity
{
    public required string Email { get; init; }
    public Guid? UserId { get; init; }
    public required string TokenHash { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
