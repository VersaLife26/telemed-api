using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Vault;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Admin.Doctors;

public sealed class AdminDoctorService(
    ICurrentActor actor,
    IDoctorRepository doctors,
    IDoctorDocumentRepository documents,
    IUserAccounts accounts,
    IFileStorage storage,
    IBillingSettingsRepository billing,
    VaultAccessPolicy access,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public async Task<PagedResult<AdminDoctorListItemDto>> ListAsync(AdminDoctorQuery query, CancellationToken ct)
    {
        var search = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        var (items, total) = await doctors.ListForAdminAsync(query.Status, search, query.Skip, query.PageSize, ct);
        return new PagedResult<AdminDoctorListItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<AdminDoctorDto> GetAsync(Guid id, CancellationToken ct) => await ToDtoAsync(await LoadAsync(id, ct), ct);

    public async Task<SignedUrlDto> GetDocumentUrlAsync(Guid id, Guid documentId, CancellationToken ct)
    {
        var document = await documents.FindLiveAsync(documentId, ct);
        if (document is null || document.DoctorId != id)
        {
            throw new NotFoundException("Document not found.");
        }

        var doctor = await LoadAsync(id, ct);
        access.Record(RecordResourceType.DoctorDocument, document.Id, doctor.UserId, RecordAccessAction.View, granted: true, VaultAccessPolicy.AdminReviewReason);
        await unitOfWork.SaveChangesAsync(ct);
        return new SignedUrlDto(storage.CreateSignedUrl(document.StorageKey, document.ContentType));
    }

    public async Task<AdminDoctorDto> SuspendAsync(Guid id, SuspendDoctorRequest request, CancellationToken ct)
    {
        var doctor = await LoadAsync(id, ct);
        if (doctor.Status != DoctorStatus.Active)
        {
            throw new ConflictException("doctor_not_active", "Only an active doctor can be suspended.");
        }

        doctor.Status = DoctorStatus.Suspended;
        doctor.SuspendedAt = time.GetUtcNow();
        doctor.SuspendedReason = request.Reason.Trim();
        doctor.SuspendedBy = actor.RequireAdmin().Id;
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(doctor, ct);
    }

    public async Task<AdminDoctorDto> ReinstateAsync(Guid id, CancellationToken ct)
    {
        var doctor = await LoadAsync(id, ct);
        if (doctor.Status != DoctorStatus.Suspended)
        {
            throw new ConflictException("doctor_not_suspended", "Only a suspended doctor can be reinstated.");
        }

        doctor.Status = DoctorStatus.Active;
        doctor.SuspendedAt = null;
        doctor.SuspendedReason = null;
        doctor.SuspendedBy = null;
        doctor.SuspendedWithUser = false;
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(doctor, ct);
    }

    public async Task<AdminDoctorDto> SetForeignMultiplierAsync(Guid id, SetForeignMultiplierRequest request, CancellationToken ct)
    {
        var doctor = await LoadAsync(id, ct);
        doctor.ForeignMultiplier = request.Multiplier;
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(doctor, ct);
    }

    private async Task<AdminDoctorDto> ToDtoAsync(Doctor doctor, CancellationToken ct)
    {
        var dto = doctor.ToAdminDto(
            await accounts.FindByIdAsync(doctor.UserId, ct),
            await documents.ListLiveForDoctorAsync(doctor.Id, ct),
            storage);
        if ((await billing.GetAsync(ct)).LkrPerUsd is { } rate
            && doctor.ForeignMultiplier is { } multiplier
            && ForeignPricing.TryUsdCents(doctor.FeeCents, multiplier, rate) is { } usd)
        {
            return dto with { ForeignFeeCents = usd, ForeignCurrency = ForeignPricing.Currency };
        }

        return dto;
    }

    private async Task<Doctor> LoadAsync(Guid id, CancellationToken ct) =>
        await doctors.FindAsync(id, ct) ?? throw new NotFoundException("Doctor not found.");
}
