using TeleMed.Domain.Enums;

namespace TeleMed.Application.DoctorApplications;

public sealed record BankDetailsRequest(string BankName, string BranchName, string AccountNumber, string AccountName);

public sealed record DoctorApplicationRequest(
    string Phone,
    string Email,
    string? Password,
    string FirstName,
    string LastName,
    string? DisplayName,
    string SlmcNumber,
    string SpecialtyCode,
    IReadOnlyList<ConsultationLanguage> Languages,
    string? LanguageOther,
    int ExperienceYears,
    long FeeCents,
    string? Bio,
    bool PgimBoardCertified,
    bool IsGeneralPractitioner,
    string MedicalSchool,
    string QualificationsText,
    string AvailabilityNotes,
    IReadOnlyList<string> PracticingLocations,
    bool TermsAccepted,
    BankDetailsRequest Bank);

public sealed record DoctorApplicationCreatedDto(Guid Id, string UploadToken);

public sealed record EligibilityQuery
{
    public string Phone { get; init; } = "";
}

public enum ApplicantEligibility
{
    None,
    Pending,
    UnderReview,
    Approved,
    Rejected,
    Doctor,
}

public sealed record EligibilityDto(ApplicantEligibility Status, Guid? ApplicationId, Guid? DoctorId);

internal sealed record BankAccount(string AccountNumber, string AccountName);
