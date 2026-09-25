using TeleMed.Application.Abstractions;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Scheduling;
using TeleMed.Application.Users;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Doctors;

public sealed class DoctorSelfService(
    ICurrentActor actor,
    IDoctorRepository doctors,
    IDoctorDocumentRepository documents,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    private static readonly HashSet<DoctorDocumentType> CredentialTypes =
    [
        DoctorDocumentType.SlmcCertificate,
        DoctorDocumentType.Nic,
        DoctorDocumentType.DegreeCertificate,
        DoctorDocumentType.SpecialtyBoardCertificate,
        DoctorDocumentType.Other,
    ];

    public async Task<DoctorProfileDto> GetAsync(CancellationToken ct) => (await doctors.MineAsync(actor, ct)).ToProfileDto(storage);

    public async Task<DoctorProfileDto> UpdateAsync(UpdateDoctorProfileRequest request, CancellationToken ct)
    {
        var doctor = await doctors.MineWritableAsync(actor, ct);
        if (request.Version != doctor.Version)
        {
            throw new ConflictException("concurrency_conflict", "Your profile was changed elsewhere. Reload and try again.");
        }

        doctor.DisplayName = request.DisplayName.Trim();
        doctor.Bio = request.Bio?.Trim() ?? "";
        doctor.SubSpecialties = request.SubSpecialties?.Select(s => s.Trim()).ToList() ?? [];
        doctor.Languages = request.Languages.ToList();
        doctor.LanguageOther = string.IsNullOrWhiteSpace(request.LanguageOther) ? null : request.LanguageOther.Trim();
        doctor.Qualifications = request.Qualifications?.Select(q => q.ToQualification()).ToList() ?? [];
        doctor.ExperienceYears = request.ExperienceYears;
        doctor.FeeCents = request.FeeCents;
        doctor.AcceptsNewPatients = request.AcceptsNewPatients;
        await unitOfWork.SaveChangesAsync(ct);
        return doctor.ToProfileDto(storage);
    }

    public async Task<PhotoUrlDto> GetPhotoUrlAsync(CancellationToken ct) =>
        DoctorMapper.PhotoUrl(await doctors.MineAsync(actor, ct), storage) is { } url ? new PhotoUrlDto(url) : throw new NotFoundException("No profile photo.");

    public async Task<DoctorProfileDto> SetPhotoAsync(Stream content, CancellationToken ct)
    {
        var doctor = await doctors.MineWritableAsync(actor, ct);
        var upload = await DoctorFiles.ReadAsync(content, PlatformPolicy.ProfilePhotoMaxBytes, FileSignature.Images, ct);
        var key = $"doctors/{doctor.Id}/photo-{Guid.CreateVersion7()}{FileSignature.Extension(upload.ContentType)}";
        using (var stream = new MemoryStream(upload.Bytes, writable: false))
        {
            await storage.SaveAsync(key, stream, ct);
        }

        var previous = doctor.PhotoStorageKey;
        doctor.PhotoStorageKey = key;
        await unitOfWork.SaveChangesAsync(ct);
        if (previous is not null)
        {
            await storage.DeleteAsync(previous, ct);
        }

        return doctor.ToProfileDto(storage);
    }

    public async Task DeletePhotoAsync(CancellationToken ct)
    {
        var doctor = await doctors.MineWritableAsync(actor, ct);
        if (doctor.PhotoStorageKey is not { } previous)
        {
            return;
        }

        doctor.PhotoStorageKey = null;
        await unitOfWork.SaveChangesAsync(ct);
        await storage.DeleteAsync(previous, ct);
    }

    public async Task<SignedUrlDto> GetStampUrlAsync(DoctorDocumentType type, CancellationToken ct)
    {
        var doctor = await doctors.MineAsync(actor, ct);
        var document = await documents.FindLiveForDoctorAsync(doctor.Id, type, ct)
            ?? throw new NotFoundException($"No {DocumentTypes.Name(type)} uploaded.");
        return new SignedUrlDto(storage.CreateSignedUrl(document.StorageKey, document.ContentType));
    }

    public async Task<DoctorDocumentDto> SetStampAsync(DoctorDocumentType type, Stream content, string? fileName, CancellationToken ct)
    {
        var doctor = await doctors.MineWritableAsync(actor, ct);
        var upload = await DoctorFiles.ReadAsync(content, PlatformPolicy.DoctorDocumentMaxBytes, DoctorFiles.Stamps, ct);
        var document = await DoctorFiles.StoreAsync(storage, $"doctors/{doctor.Id}", null, doctor.Id, type, upload, fileName, ct);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        if (await documents.FindLiveForDoctorAsync(doctor.Id, type, ct) is { } previous)
        {
            previous.DeletedAt = time.GetUtcNow();
            // The unique index on live signature/seal rows is filtered, so EF cannot order the retire before the insert itself.
            await unitOfWork.SaveChangesAsync(ct);
        }

        documents.Add(document);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return document.ToDto();
    }

    public async Task<IReadOnlyList<DoctorDocumentDto>> ListDocumentsAsync(CancellationToken ct)
    {
        var doctor = await doctors.MineAsync(actor, ct);
        return (await documents.ListLiveForDoctorAsync(doctor.Id, ct)).Select(d => d.ToDownloadableDto(storage)).ToList();
    }

    public async Task<DoctorDocumentDto> UploadDocumentAsync(string type, Stream content, string? fileName, CancellationToken ct)
    {
        var documentType = DocumentTypes.Parse(type);
        if (!CredentialTypes.Contains(documentType))
        {
            throw DoctorFiles.Invalid("Signatures and seals have their own endpoints.", "type");
        }

        var doctor = await doctors.MineWritableAsync(actor, ct);
        var upload = await DoctorFiles.ReadAsync(content, PlatformPolicy.DoctorDocumentMaxBytes, DoctorFiles.Documents, ct);
        var document = await DoctorFiles.StoreAsync(storage, $"doctors/{doctor.Id}", null, doctor.Id, documentType, upload, fileName, ct);
        documents.Add(document);
        await unitOfWork.SaveChangesAsync(ct);
        return document.ToDto();
    }
}
