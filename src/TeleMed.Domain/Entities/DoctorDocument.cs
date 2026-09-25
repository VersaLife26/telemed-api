using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class DoctorDocument : Entity, IAuditable
{
    public Guid? ApplicationId { get; init; }
    public Guid? DoctorId { get; set; }
    public DoctorDocumentType Type { get; init; }
    public required string StorageKey { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public long SizeBytes { get; init; }
    public required string Sha256 { get; init; }
    public DateTimeOffset? DeletedAt { get; set; }
}
