using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.ClinicalNotes;

public sealed record DiagnosisDto(string Code, string Display, bool IsPrimary);

public sealed record DiagnosisInput(string Code, bool IsPrimary);

public sealed record ClinicalNoteDto(
    Guid Id,
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    string Subjective,
    string Objective,
    string Assessment,
    string Plan,
    ClinicalNoteStatus Status,
    DateTimeOffset? FinalisedAt,
    int Revision,
    uint Version,
    IReadOnlyList<DiagnosisDto> Diagnoses,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ClinicalNoteSummaryDto(
    Guid Id,
    Guid AppointmentId,
    Guid PatientId,
    string PatientName,
    DateTimeOffset AppointmentStartAt,
    ClinicalNoteStatus Status,
    DateTimeOffset? FinalisedAt,
    int Revision,
    DateTimeOffset UpdatedAt);

public sealed record ClinicalNoteRevisionDto(
    int Revision,
    string Subjective,
    string Objective,
    string Assessment,
    string Plan,
    IReadOnlyList<DiagnosisDto> Diagnoses,
    ClinicalNoteChangeType ChangeType,
    string? AmendmentReason,
    Guid ChangedBy,
    DateTimeOffset CreatedAt);

// Version is the value last read; a stale one is rejected so two devices cannot overwrite each other silently.
public sealed record SaveClinicalNoteRequest(
    string? Subjective, string? Objective, string? Assessment, string? Plan, IReadOnlyList<DiagnosisInput>? Diagnoses, uint? Version);

public sealed record FinaliseClinicalNoteRequest(uint? Version);

// Sections and diagnoses left null keep their current value.
public sealed record AmendClinicalNoteRequest(
    string Reason, string? Subjective, string? Objective, string? Assessment, string? Plan, IReadOnlyList<DiagnosisInput>? Diagnoses, uint? Version);

public sealed record ClinicalNoteQuery : PageQuery
{
    public ClinicalNoteStatus? Status { get; init; }
}
