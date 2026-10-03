using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Doctors;

public sealed record QualificationDto(string Degree, string Institution, int? Year);

public sealed record DoctorDocumentDto(
    Guid Id,
    DoctorDocumentType Type,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DateTimeOffset CreatedAt)
{
    public string? DownloadUrl { get; init; }
}

public sealed record SignedUrlDto(string Url);

public sealed record PublicDoctorDto
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = "";
    public string SpecialtyCode { get; init; } = "";
    public IReadOnlyList<string> SubSpecialties { get; init; } = [];
    public string Bio { get; init; } = "";
    public IReadOnlyList<ConsultationLanguage> Languages { get; init; } = [];
    public string? LanguageOther { get; init; }
    public IReadOnlyList<QualificationDto> Qualifications { get; init; } = [];
    public int ExperienceYears { get; init; }
    public long FeeCents { get; init; }
    public string Currency { get; init; } = "";
    public long? ForeignFeeCents { get; init; }
    public string? ForeignCurrency { get; init; }
    public bool AcceptsNewPatients { get; init; }
    public string? PhotoUrl { get; init; }
}

public sealed record DoctorProfileDto
{
    public Guid Id { get; init; }
    public string SlmcNumber { get; init; } = "";
    public string SpecialtyCode { get; init; } = "";
    public IReadOnlyList<string> SubSpecialties { get; init; } = [];
    public string DisplayName { get; init; } = "";
    public string Bio { get; init; } = "";
    public IReadOnlyList<ConsultationLanguage> Languages { get; init; } = [];
    public string? LanguageOther { get; init; }
    public IReadOnlyList<QualificationDto> Qualifications { get; init; } = [];
    public int ExperienceYears { get; init; }
    public long FeeCents { get; init; }
    public string Currency { get; init; } = "";
    public bool AcceptsNewPatients { get; init; }
    public string BankName { get; init; } = "";
    public string BankBranch { get; init; } = "";
    public string? PhotoUrl { get; init; }
    public DoctorStatus Status { get; init; }
    public string? SuspendedReason { get; init; }
    public uint Version { get; init; }
}

public sealed record UpdateDoctorProfileRequest(
    string DisplayName,
    string? Bio,
    IReadOnlyList<string>? SubSpecialties,
    IReadOnlyList<ConsultationLanguage> Languages,
    string? LanguageOther,
    IReadOnlyList<QualificationDto>? Qualifications,
    int ExperienceYears,
    long FeeCents,
    bool AcceptsNewPatients,
    uint Version);

public enum DoctorSort
{
    Name,
    Fee,
    Experience,
}

public sealed record DoctorSearchQuery : PageQuery
{
    public string? Specialty { get; init; }
    public ConsultationLanguage? Language { get; init; }
    public string? Q { get; init; }
    public long? MinFee { get; init; }
    public long? MaxFee { get; init; }
    public DoctorSort? Sort { get; init; }
}

public sealed record DoctorSearchFilter(
    string? Specialty,
    ConsultationLanguage? Language,
    string? Text,
    long? MinFee,
    long? MaxFee,
    DoctorSort? Sort,
    int Skip,
    int Take);
