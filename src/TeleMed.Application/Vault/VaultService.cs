using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Scheduling;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Vault;

public sealed class VaultService(
    ICurrentActor actor,
    IDoctorRepository doctors,
    IVaultRepository vault,
    IFileStorage storage,
    VaultAccessPolicy policy,
    IRecordAccessRepository records,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    private static readonly IReadOnlySet<string> Allowed =
        new HashSet<string> { FileSignature.Pdf, FileSignature.Png, FileSignature.Jpeg, FileSignature.Webp };

    public async Task<PagedResult<VaultDocumentDto>> ListDocumentsAsync(VaultDocumentQuery query, CancellationToken ct)
    {
        var ownerId = OwnerFor(query.PatientId);
        await policy.AuthorizeAsync(new RecordAccessRequest(RecordResourceType.VaultDocument, null, ownerId, RecordAccessAction.List), ct);

        FolderFilter? inFolder = null;
        if (query.FolderId is { } folderRef)
        {
            inFolder = new FolderFilter(FolderRef.Parse(folderRef));
            if (inFolder.Folder is { } folderId)
            {
                await RequireFolderAsync(folderId, ownerId, ct);
            }
        }

        var (items, total) = await vault.ListLiveDocumentsAsync(ownerId, inFolder, query.DocumentType, query.Skip, query.PageSize, ct);
        return new PagedResult<VaultDocumentDto>(items.Select(d => d.ToDto()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<VaultDocumentDto> UploadAsync(VaultUploadForm form, Stream content, string? fileName, CancellationToken ct)
    {
        var ownerId = OwnerFor(form.PatientId);
        var id = Guid.CreateVersion7();
        await policy.AuthorizeAsync(new RecordAccessRequest(RecordResourceType.VaultDocument, id, ownerId, RecordAccessAction.Upload), ct);
        if (form.FolderId is { } folderId)
        {
            await RequireFolderAsync(folderId, ownerId, ct);
        }

        var name = Path.GetFileName(fileName ?? "").Trim();
        if (!VaultNames.IsValidFileName(name))
        {
            throw DoctorFiles.Invalid($"File names must be 1-{VaultDocument.MaxFileNameLength} characters without slashes.");
        }

        var upload = await DoctorFiles.ReadAsync(content, PlatformPolicy.VaultMaxDocumentBytes, Allowed, ct);
        if (FileSignature.ContentTypeFromExtension(Path.GetExtension(name)) != upload.ContentType)
        {
            throw DoctorFiles.Invalid("The file extension does not match its contents.");
        }

        var key = $"vault/{ownerId}/{id}{FileSignature.Extension(upload.ContentType)}";
        using (var stream = new MemoryStream(upload.Bytes, writable: false))
        {
            await storage.SaveAsync(key, stream, ct);
        }

        var document = new VaultDocument
        {
            Id = id,
            OwnerId = ownerId,
            UploadedBy = actor.RequireUserId(),
            FolderId = form.FolderId,
            DocumentType = form.DocumentType,
            StorageKey = key,
            FileName = name,
            ContentType = upload.ContentType,
            SizeBytes = upload.Bytes.LongLength,
            Sha256 = upload.Sha256,
        };
        vault.AddDocument(document);
        await unitOfWork.SaveChangesAsync(ct);
        return document.ToDto();
    }

    public async Task<VaultDocumentDto> GetDocumentAsync(Guid id, CancellationToken ct)
    {
        var document = await vault.FindLiveDocumentAsync(id, ct) ?? throw DocumentNotFound();
        await policy.AuthorizeAsync(new RecordAccessRequest(RecordResourceType.VaultDocument, id, document.OwnerId, RecordAccessAction.View), ct);
        return document.ToDto();
    }

    public async Task<VaultDownloadDto> DownloadAsync(Guid id, CancellationToken ct)
    {
        var document = await vault.FindLiveDocumentAsync(id, ct) ?? throw DocumentNotFound();
        await policy.AuthorizeAsync(new RecordAccessRequest(RecordResourceType.VaultDocument, id, document.OwnerId, RecordAccessAction.Download), ct);
        return new VaultDownloadDto(
            storage.CreateSignedUrl(document.StorageKey, document.ContentType),
            (int)storage.SignedUrlLifetime.TotalSeconds,
            document.FileName,
            document.ContentType);
    }

    public async Task<VaultDocumentDto> UpdateDocumentAsync(Guid id, UpdateVaultDocumentRequest request, CancellationToken ct)
    {
        var document = await vault.FindLiveDocumentForUpdateAsync(id, ct) ?? throw DocumentNotFound();
        await policy.AuthorizeAsync(new RecordAccessRequest(RecordResourceType.VaultDocument, id, document.OwnerId, RecordAccessAction.Amend), ct);

        if (request.FileName is { } fileName)
        {
            var name = fileName.Trim();
            var extension = Path.GetExtension(document.FileName);
            if (!string.Equals(Path.GetExtension(name), extension, StringComparison.OrdinalIgnoreCase))
            {
                name += extension;
            }

            document.FileName = VaultNames.IsValidFileName(name)
                ? name
                : throw DoctorFiles.Invalid($"File names must be 1-{VaultDocument.MaxFileNameLength} characters without slashes.", "fileName");
        }

        if (request.FolderId is { } folderRef)
        {
            var folderId = FolderRef.Parse(folderRef);
            if (folderId is { } target)
            {
                await RequireFolderAsync(target, document.OwnerId, ct);
            }

            document.FolderId = folderId;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return document.ToDto();
    }

    public async Task DeleteDocumentAsync(Guid id, CancellationToken ct)
    {
        var document = await vault.FindLiveDocumentForUpdateAsync(id, ct) ?? throw DocumentNotFound();
        await policy.AuthorizeAsync(new RecordAccessRequest(RecordResourceType.VaultDocument, id, document.OwnerId, RecordAccessAction.Delete), ct);
        document.DeletedAt = time.GetUtcNow();
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AccessiblePatientDto>> ListAccessiblePatientsAsync(CancellationToken ct)
    {
        var doctor = await doctors.MineAsync(actor, ct);
        var now = time.GetUtcNow();
        var patients = await records.ListTreatedPatientsAsync(doctor.Id, TreatingAccess.LastSeenCutoff(now), ct);
        return patients
            .Select(p => new AccessiblePatientDto(p.PatientId, p.FullName, p.LastSeenAt, p.LastSeenAt + PlatformPolicy.TreatingAccessWindow))
            .ToList();
    }

    public async Task<IReadOnlyList<VaultFolderDto>> ListFoldersAsync(VaultFolderQuery query, CancellationToken ct)
    {
        var ownerId = OwnerFor(query.PatientId);
        await policy.AuthorizeAsync(new RecordAccessRequest(RecordResourceType.VaultDocument, null, ownerId, RecordAccessAction.List), ct);
        return (await vault.ListLiveFoldersAsync(ownerId, ct)).Select(f => f.ToDto()).ToList();
    }

    public async Task<VaultFolderDto> CreateFolderAsync(CreateVaultFolderRequest request, CancellationToken ct)
    {
        var ownerId = actor.RequireUserId();
        if (request.ParentId is { } parentId)
        {
            await RequireFolderAsync(parentId, ownerId, ct);
        }

        var folder = new VaultFolder { OwnerId = ownerId, ParentId = request.ParentId, Name = request.Name.Trim(), CreatedBy = ownerId };
        vault.AddFolder(folder);
        await unitOfWork.SaveChangesAsync(ct);
        return folder.ToDto();
    }

    public async Task<VaultFolderDto> UpdateFolderAsync(Guid id, UpdateVaultFolderRequest request, CancellationToken ct)
    {
        var folder = await OwnFolderForUpdateAsync(id, ct);
        if (request.Name is { } name)
        {
            folder.Name = name.Trim();
        }

        if (request.ParentId is { } parentRef)
        {
            var parentId = FolderRef.Parse(parentRef);
            if (parentId is { } target)
            {
                await RequireFolderAsync(target, folder.OwnerId, ct);
                var parents = (await vault.ListLiveFoldersAsync(folder.OwnerId, ct)).ToDictionary(f => f.Id, f => f.ParentId);
                for (Guid? current = target; current is { } step; current = parents.GetValueOrDefault(step))
                {
                    if (step == folder.Id)
                    {
                        throw new ConflictException("folder_cycle", "A folder cannot be moved inside itself.");
                    }
                }
            }

            folder.ParentId = parentId;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return folder.ToDto();
    }

    public async Task DeleteFolderAsync(Guid id, CancellationToken ct)
    {
        var folder = await OwnFolderForUpdateAsync(id, ct);
        if (!await vault.FolderIsEmptyAsync(folder.Id, ct))
        {
            throw new ConflictException("folder_not_empty", "Only an empty folder can be deleted.");
        }

        folder.DeletedAt = time.GetUtcNow();
        await unitOfWork.SaveChangesAsync(ct);
    }

    // Patients work in their own vault; doctors always name the patient whose vault they want.
    private Guid OwnerFor(Guid? patientId)
    {
        var userId = actor.RequireUserId();
        return patientId ?? (actor.Role == UserRole.Patient
            ? userId
            : throw DoctorFiles.Invalid("Choose the patient whose records you want.", "patientId"));
    }

    private async Task RequireFolderAsync(Guid folderId, Guid ownerId, CancellationToken ct)
    {
        if (await vault.FindLiveFolderAsync(folderId, ct) is not { } folder || folder.OwnerId != ownerId)
        {
            throw FolderNotFound();
        }
    }

    private async Task<VaultFolder> OwnFolderForUpdateAsync(Guid id, CancellationToken ct) =>
        await vault.FindLiveFolderForUpdateAsync(id, ct) is { } folder && folder.OwnerId == actor.RequireUserId()
            ? folder
            : throw FolderNotFound();

    private static NotFoundException DocumentNotFound() => new("Document not found.");

    private static NotFoundException FolderNotFound() => new("Folder not found.");
}
