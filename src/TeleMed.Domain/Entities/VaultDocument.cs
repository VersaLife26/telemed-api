using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class VaultDocument : Entity
{
    public const int MaxFileNameLength = 255;

    public Guid OwnerId { get; init; }
    public Guid UploadedBy { get; init; }
    public Guid? FolderId { get; set; }
    public VaultDocumentType DocumentType { get; init; }
    public required string StorageKey { get; init; }
    public required string FileName { get; set; }
    public required string ContentType { get; init; }
    public long SizeBytes { get; init; }
    public required string Sha256 { get; init; }
    public DateTimeOffset? DeletedAt { get; set; }
    public uint Version { get; init; }
}
