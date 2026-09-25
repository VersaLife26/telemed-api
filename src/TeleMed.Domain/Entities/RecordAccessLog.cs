using System.Net;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class RecordAccessLog
{
    public long Id { get; init; }
    public RecordResourceType ResourceType { get; init; }
    public Guid? ResourceId { get; init; }
    public Guid? OwnerId { get; init; }
    public Guid? ActorId { get; init; }
    public RecordActorRole ActorRole { get; init; }
    public RecordAccessAction Action { get; init; }
    public bool Granted { get; init; }
    public required string Reason { get; init; }
    public IPAddress? Ip { get; init; }
    public string? UserAgent { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
