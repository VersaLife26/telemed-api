using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

public class VaultFolder : Entity
{
    public const int MaxNameLength = 120;

    public Guid OwnerId { get; init; }
    public Guid? ParentId { get; set; }
    public required string Name { get; set; }
    public Guid CreatedBy { get; init; }
    public DateTimeOffset? DeletedAt { get; set; }
    public uint Version { get; init; }
}
