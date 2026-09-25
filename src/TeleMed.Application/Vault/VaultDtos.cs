using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Vault;

public sealed record VaultDocumentDto(
    Guid Id,
    Guid OwnerId,
    Guid UploadedBy,
    Guid? FolderId,
    VaultDocumentType DocumentType,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// FolderId is "root" for documents outside any folder, or a folder id; omitted lists every folder.
public sealed record VaultDocumentQuery : PageQuery
{
    public Guid? PatientId { get; init; }
    public string? FolderId { get; init; }
    public VaultDocumentType? DocumentType { get; init; }
}

public sealed record VaultUploadForm
{
    public Guid? PatientId { get; init; }
    public Guid? FolderId { get; init; }
    public VaultDocumentType DocumentType { get; init; } = VaultDocumentType.Other;
}

// FolderId "root" moves the document out of its folder.
public sealed record UpdateVaultDocumentRequest(string? FileName, string? FolderId);

public sealed record VaultDownloadDto(string Url, int ExpiresInSeconds, string FileName, string ContentType);

public sealed record AccessiblePatientDto(Guid PatientId, string FullName, DateTimeOffset LastConsultationAt, DateTimeOffset AccessExpiresAt);

public sealed record VaultFolderDto(Guid Id, Guid OwnerId, Guid? ParentId, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record VaultFolderQuery
{
    public Guid? PatientId { get; init; }
}

public sealed record CreateVaultFolderRequest(string Name, Guid? ParentId);

// ParentId "root" moves the folder to the top level.
public sealed record UpdateVaultFolderRequest(string? Name, string? ParentId);

public static class FolderRef
{
    public const string Root = "root";

    public static bool IsValid(string? value) => value is null || value == Root || Guid.TryParse(value, out _);

    // Null when the reference names the root.
    public static Guid? Parse(string value) => value == Root ? null : Guid.Parse(value);
}
