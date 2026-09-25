using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.ClinicalNotes;

public interface IClinicalNoteRepository
{
    void Add(ClinicalNote note);
    // Both load the diagnoses in their saved order.
    Task<ClinicalNote?> FindByAppointmentAsync(Guid appointmentId, CancellationToken ct);
    Task<ClinicalNote?> FindForUpdateByAppointmentAsync(Guid appointmentId, CancellationToken ct);
    Task<(IReadOnlyList<ClinicalNoteSummaryDto> Items, long Total)> ListForDoctorAsync(
        Guid doctorId, ClinicalNoteStatus? status, int skip, int take, CancellationToken ct);

    void AddRevision(ClinicalNoteRevision revision);
    // Oldest first.
    Task<IReadOnlyList<ClinicalNoteRevision>> ListRevisionsAsync(Guid noteId, CancellationToken ct);
}
