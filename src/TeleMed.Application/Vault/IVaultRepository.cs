using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Vault;

public interface IVaultRepository
{
    void AddDocument(VaultDocument document);
    Task<VaultDocument?> FindLiveDocumentAsync(Guid id, CancellationToken ct);
    Task<VaultDocument?> FindLiveDocumentForUpdateAsync(Guid id, CancellationToken ct);
    // Newest first. inFolder: null lists every folder; otherwise only that folder, where a null Folder means the root.
    Task<(IReadOnlyList<VaultDocument> Items, long Total)> ListLiveDocumentsAsync(
        Guid ownerId, FolderFilter? inFolder, VaultDocumentType? type, int skip, int take, CancellationToken ct);

    void AddFolder(VaultFolder folder);
    Task<VaultFolder?> FindLiveFolderAsync(Guid id, CancellationToken ct);
    Task<VaultFolder?> FindLiveFolderForUpdateAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<VaultFolder>> ListLiveFoldersAsync(Guid ownerId, CancellationToken ct);
    // No live documents and no live subfolders.
    Task<bool> FolderIsEmptyAsync(Guid id, CancellationToken ct);
}

public sealed record FolderFilter(Guid? Folder);
