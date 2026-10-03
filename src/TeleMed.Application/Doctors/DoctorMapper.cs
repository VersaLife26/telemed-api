using Riok.Mapperly.Abstractions;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Doctors;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class DoctorMapper
{
    public static PublicDoctorDto ToPublicDto(this Doctor doctor, IFileStorage storage, decimal? lkrPerUsd = null)
    {
        var dto = doctor.MapPublic() with { PhotoUrl = PhotoUrl(doctor, storage) };
        if (lkrPerUsd is { } rate && doctor.ForeignMultiplier is { } multiplier
            && ForeignPricing.TryUsdCents(doctor.FeeCents, multiplier, rate) is { } usd)
        {
            return dto with { ForeignFeeCents = usd, ForeignCurrency = ForeignPricing.Currency };
        }

        return dto;
    }

    public static DoctorProfileDto ToProfileDto(this Doctor doctor, IFileStorage storage) =>
        doctor.MapProfile() with { PhotoUrl = PhotoUrl(doctor, storage) };

    [MapperIgnoreTarget(nameof(DoctorDocumentDto.DownloadUrl))]
    public static partial DoctorDocumentDto ToDto(this DoctorDocument document);

    public static DoctorDocumentDto ToDownloadableDto(this DoctorDocument document, IFileStorage storage) =>
        document.ToDto() with { DownloadUrl = storage.CreateSignedUrl(document.StorageKey, document.ContentType) };

    public static partial QualificationDto ToDto(this Qualification qualification);

    public static partial Qualification ToQualification(this QualificationDto dto);

    public static string? PhotoUrl(Doctor doctor, IFileStorage storage) =>
        doctor.PhotoStorageKey is { } key ? storage.CreateSignedUrl(key, DoctorFiles.ContentType(key)) : null;

    [MapperIgnoreTarget(nameof(PublicDoctorDto.PhotoUrl))]
    [MapperIgnoreTarget(nameof(PublicDoctorDto.ForeignFeeCents))]
    [MapperIgnoreTarget(nameof(PublicDoctorDto.ForeignCurrency))]
    private static partial PublicDoctorDto MapPublic(this Doctor doctor);

    [MapperIgnoreTarget(nameof(DoctorProfileDto.PhotoUrl))]
    private static partial DoctorProfileDto MapProfile(this Doctor doctor);
}
