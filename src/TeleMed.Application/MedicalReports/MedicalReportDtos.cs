using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.MedicalReports;

public sealed record IssueMedicalReportRequest(
    string? Addressee,
    string ClinicalImpression,
    string? Findings,
    string? Advice,
    FitnessForWork Fitness,
    DateOnly? LeaveFrom,
    DateOnly? LeaveUntil,
    DateOnly? ReturnToWorkOn,
    string? FitnessNotes);

public sealed record CancelMedicalReportRequest(string? Reason);

public sealed record MedicalReportDto(
    Guid Id,
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    string DoctorName,
    string DoctorSlmc,
    string DoctorQualifications,
    DateTimeOffset IssuedAt,
    MedicalReportStatus Status,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    bool IsTest,
    string Addressee,
    string ClinicalImpression,
    string? Findings,
    string? Advice,
    FitnessForWork Fitness,
    DateOnly? LeaveFrom,
    DateOnly? LeaveUntil,
    DateOnly? ReturnToWorkOn,
    string? FitnessNotes);

public sealed record MedicalReportQuery : PageQuery;

public sealed record MedicalReportPdfFile(byte[] Content, string FileName);

public sealed record MedicalReportVerificationDto(
    bool Valid,
    string? Reason,
    DateTimeOffset? IssuedAt,
    string? DoctorName,
    string? DoctorSlmc,
    string? PatientInitials,
    string? ClinicalImpression,
    FitnessForWork? Fitness)
{
    public const string InvalidReason = "invalid";
    public const string TestReason = "test";
    public const string CancelledReason = "cancelled";

    public static MedicalReportVerificationDto Invalid { get; } =
        new(false, InvalidReason, null, null, null, null, null, null);
}
