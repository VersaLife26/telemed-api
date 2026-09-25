using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Vault;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class VaultRepository(AppDbContext db) : IVaultRepository
{
    public void AddDocument(VaultDocument document) => db.VaultDocuments.Add(document);

    public Task<VaultDocument?> FindLiveDocumentAsync(Guid id, CancellationToken ct) =>
        db.VaultDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, ct);

    public Task<VaultDocument?> FindLiveDocumentForUpdateAsync(Guid id, CancellationToken ct) =>
        db.VaultDocuments.SingleOrDefaultAsync(d => d.Id == id && d.DeletedAt == null, ct);

    public async Task<(IReadOnlyList<VaultDocument> Items, long Total)> ListLiveDocumentsAsync(
        Guid ownerId, FolderFilter? inFolder, VaultDocumentType? type, int skip, int take, CancellationToken ct)
    {
        var query = db.VaultDocuments.AsNoTracking().Where(d => d.OwnerId == ownerId && d.DeletedAt == null);
        if (inFolder is not null)
        {
            var folderId = inFolder.Folder;
            query = query.Where(d => d.FolderId == folderId);
        }

        if (type is { } documentType)
        {
            query = query.Where(d => d.DocumentType == documentType);
        }

        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public void AddFolder(VaultFolder folder) => db.VaultFolders.Add(folder);

    public Task<VaultFolder?> FindLiveFolderAsync(Guid id, CancellationToken ct) =>
        db.VaultFolders.AsNoTracking().SingleOrDefaultAsync(f => f.Id == id && f.DeletedAt == null, ct);

    public Task<VaultFolder?> FindLiveFolderForUpdateAsync(Guid id, CancellationToken ct) =>
        db.VaultFolders.SingleOrDefaultAsync(f => f.Id == id && f.DeletedAt == null, ct);

    public async Task<IReadOnlyList<VaultFolder>> ListLiveFoldersAsync(Guid ownerId, CancellationToken ct) =>
        await db.VaultFolders.AsNoTracking()
            .Where(f => f.OwnerId == ownerId && f.DeletedAt == null)
            .OrderBy(f => f.Name).ThenBy(f => f.Id)
            .ToListAsync(ct);

    public async Task<bool> FolderIsEmptyAsync(Guid id, CancellationToken ct) =>
        !await db.VaultDocuments.AnyAsync(d => d.FolderId == id && d.DeletedAt == null, ct)
        && !await db.VaultFolders.AnyAsync(f => f.ParentId == id && f.DeletedAt == null, ct);
}
