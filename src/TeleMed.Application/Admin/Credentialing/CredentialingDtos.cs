using TeleMed.Application.Common;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Credentialing;

public sealed record DoctorApplicationQuery : PageQuery
{
    public DoctorApplicationStatus? Status { get; init; }
}

public sealed record DoctorApplicationSummaryDto(
    Guid Id,
    string DisplayName,
    string Phone,
    string Email,
    string SlmcNumber,
    string SpecialtyCode,
    DoctorApplicationStatus Status,
    DateTimeOffset CreatedAt);

public sealed record ChecklistItemDto(bool Ok, Guid ByAdminId, DateTimeOffset At);

public sealed record ChecklistDto(
    ChecklistItemDto? SlmcFormat,
    ChecklistItemDto? SlmcRegistry,
    ChecklistItemDto? Experience,
    ChecklistItemDto? NicMatch,
    ChecklistItemDto? PhotoClarity);

public sealed record DoctorApplicationDto
{
    public Guid Id { get; init; }
    public string Phone { get; init; } = "";
    public string Email { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string SlmcNumber { get; init; } = "";
    public string SpecialtyCode { get; init; } = "";
    public IReadOnlyList<ConsultationLanguage> Languages { get; init; } = [];
    public string? LanguageOther { get; init; }
    public int ExperienceYears { get; init; }
    public long FeeCents { get; init; }
    public string Bio { get; init; } = "";
    public bool PgimBoardCertified { get; init; }
    public bool IsGeneralPractitioner { get; init; }
    public string MedicalSchool { get; init; } = "";
    public string QualificationsText { get; init; } = "";
    public string AvailabilityNotes { get; init; } = "";
    public IReadOnlyList<string> PracticingLocations { get; init; } = [];
    public DateTimeOffset TermsAcceptedAt { get; init; }
    public string BankName { get; init; } = "";
    public string BankBranch { get; init; } = "";
    public bool HasPassword { get; init; }
    public DoctorApplicationStatus Status { get; init; }
    public ChecklistDto Checklist { get; init; } = new(null, null, null, null, null);
    public DateTimeOffset? ReviewStartedAt { get; init; }
    public Guid? ReviewStartedBy { get; init; }
    public string? RejectionReason { get; init; }
    public DateTimeOffset? DecidedAt { get; init; }
    public Guid? DecidedBy { get; init; }
    public Guid? DoctorId { get; init; }
    public IReadOnlyList<DoctorDocumentDto> Documents { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record UpdateChecklistRequest(bool? SlmcFormat, bool? SlmcRegistry, bool? Experience, bool? NicMatch, bool? PhotoClarity);

public sealed record RejectApplicationRequest(string Reason);
