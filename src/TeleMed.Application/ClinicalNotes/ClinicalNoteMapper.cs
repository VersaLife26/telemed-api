using TeleMed.Domain.Entities;

namespace TeleMed.Application.ClinicalNotes;

internal static class ClinicalNoteMapper
{
    public static ClinicalNoteDto ToDto(this ClinicalNote n) => new(
        n.Id,
        n.AppointmentId,
        n.DoctorId,
        n.PatientId,
        n.Subjective,
        n.Objective,
        n.Assessment,
        n.Plan,
        n.Status,
        n.FinalisedAt,
        n.Revision,
        n.Version,
        n.Diagnoses.OrderBy(d => d.SortOrder).Select(d => new DiagnosisDto(d.Code, d.Display, d.IsPrimary)).ToList(),
        n.CreatedAt,
        n.UpdatedAt);

    public static ClinicalNoteRevisionDto ToDto(this ClinicalNoteRevision r) => new(
        r.Revision,
        r.Subjective,
        r.Objective,
        r.Assessment,
        r.Plan,
        r.Diagnoses.Select(d => new DiagnosisDto(d.Code, d.Display, d.IsPrimary)).ToList(),
        r.ChangeType,
        r.AmendmentReason,
        r.ChangedBy,
        r.CreatedAt);
}
