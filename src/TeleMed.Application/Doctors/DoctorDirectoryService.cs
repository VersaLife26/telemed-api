using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Users;

namespace TeleMed.Application.Doctors;

public sealed class DoctorDirectoryService(IDoctorRepository doctors, IBillingSettingsRepository billing, IFileStorage storage)
{
    public async Task<PagedResult<PublicDoctorDto>> SearchAsync(DoctorSearchQuery query, CancellationToken ct)
    {
        var filter = new DoctorSearchFilter(
            string.IsNullOrWhiteSpace(query.Specialty) ? null : query.Specialty.Trim(),
            query.Language,
            string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim(),
            query.MinFee,
            query.MaxFee,
            query.Sort,
            query.Skip,
            query.PageSize);
        var (items, total) = await doctors.SearchListedAsync(filter, ct);
        var rate = await LkrPerUsdAsync(ct);
        return new PagedResult<PublicDoctorDto>(items.Select(d => d.ToPublicDto(storage, rate)).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<PublicDoctorDto> GetAsync(Guid id, CancellationToken ct) =>
        (await LoadListedAsync(id, ct)).ToPublicDto(storage, await LkrPerUsdAsync(ct));

    public async Task<PhotoUrlDto> GetPhotoUrlAsync(Guid id, CancellationToken ct)
    {
        var doctor = await LoadListedAsync(id, ct);
        return DoctorMapper.PhotoUrl(doctor, storage) is { } url ? new PhotoUrlDto(url) : throw new NotFoundException("No profile photo.");
    }

    private async Task<Domain.Entities.Doctor> LoadListedAsync(Guid id, CancellationToken ct) =>
        await doctors.FindListedAsync(id, ct) ?? throw new NotFoundException("Doctor not found.");

    private async Task<decimal?> LkrPerUsdAsync(CancellationToken ct) => (await billing.GetAsync(ct)).LkrPerUsd;
}
