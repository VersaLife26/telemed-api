using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

public class RefreshToken : Entity
{
    public Guid UserId { get; init; }
    public Guid FamilyId { get; init; }
    public required string TokenHash { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
}
