using TeleMed.Application.Common;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Doctors;

public sealed record AdminDoctorQuery : PageQuery
{
    public DoctorStatus? Status { get; init; }
    public string? Q { get; init; }
}

public sealed record AdminDoctorListItemDto(
    Guid Id,
    string DisplayName,
    string SlmcNumber,
    string SpecialtyCode,
    DoctorStatus Status,
    string? Phone,
    string? Email,
    long FeeCents,
    int? CommissionBps,
    DateTimeOffset CreatedAt);

public sealed record AdminDoctorDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid? ApplicationId { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
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
    public int? CommissionBps { get; init; }
    public string Currency { get; init; } = "";
    public bool AcceptsNewPatients { get; init; }
    public string BankName { get; init; } = "";
    public string BankBranch { get; init; } = "";
    public string? PhotoUrl { get; init; }
    public DoctorStatus Status { get; init; }
    public DateTimeOffset ApprovedAt { get; init; }
    public Guid ApprovedBy { get; init; }
    public DateTimeOffset? SuspendedAt { get; init; }
    public string? SuspendedReason { get; init; }
    public Guid? SuspendedBy { get; init; }
    public bool SuspendedWithUser { get; init; }
    public IReadOnlyList<DoctorDocumentDto> Documents { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record SuspendDoctorRequest(string Reason);
