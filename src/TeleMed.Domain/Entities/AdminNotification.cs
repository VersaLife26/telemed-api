using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class AdminNotification : Entity
{
    public AdminNotificationKind Kind { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? Href { get; init; }
    public Guid? ResourceId { get; init; }
    public DateTimeOffset? ReadAt { get; set; }
}
