using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class WaitingRoomItem : Entity, IAuditable
{
    public WaitingRoomItemKind Kind { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    public string? LinkUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string? ImageStorageKey { get; set; }
    public string? VideoStorageKey { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
