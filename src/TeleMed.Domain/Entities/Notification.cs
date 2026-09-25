using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class Notification : Entity
{
    public Guid? UserId { get; init; }
    public MessageChannel Channel { get; init; }
    public required string TemplateKey { get; init; }
    public Language Locale { get; init; }
    public required string Recipient { get; init; }
    public string? Subject { get; init; }
    // Nulled by housekeeping once a sent message is old enough that only the delivery record matters.
    public string? Body { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public required string DedupeKey { get; init; }
}
