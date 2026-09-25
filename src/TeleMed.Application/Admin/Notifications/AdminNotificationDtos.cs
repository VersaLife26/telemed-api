using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Notifications;

public sealed record AdminNotificationQuery : PageQuery
{
    public bool UnreadOnly { get; init; }
}

public sealed record AdminNotificationDto(
    Guid Id,
    AdminNotificationKind Kind,
    string Title,
    string Body,
    string? Href,
    Guid? ResourceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record UnreadCountDto(long Count);
