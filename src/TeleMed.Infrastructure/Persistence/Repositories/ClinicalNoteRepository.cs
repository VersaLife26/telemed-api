using Microsoft.EntityFrameworkCore;
using TeleMed.Application.ClinicalNotes;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class ClinicalNoteRepository(AppDbContext db) : IClinicalNoteRepository
{
    public void Add(ClinicalNote note) => db.ClinicalNotes.Add(note);

    public Task<ClinicalNote?> FindByAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        WithDiagnoses().AsNoTracking().SingleOrDefaultAsync(n => n.AppointmentId == appointmentId, ct);

    public Task<ClinicalNote?> FindForUpdateByAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        WithDiagnoses().SingleOrDefaultAsync(n => n.AppointmentId == appointmentId, ct);

    public async Task<(IReadOnlyList<ClinicalNoteSummaryDto> Items, long Total)> ListForDoctorAsync(
        Guid doctorId, ClinicalNoteStatus? status, int skip, int take, CancellationToken ct)
    {
        var query = from n in db.ClinicalNotes
                    join a in db.Appointments on n.AppointmentId equals a.Id
                    where n.DoctorId == doctorId && (status == null || n.Status == status)
                    select new { n, a };
        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.n.UpdatedAt).ThenBy(x => x.n.Id)
            .Skip(skip).Take(take)
            .Select(x => new ClinicalNoteSummaryDto(
                x.n.Id, x.n.AppointmentId, x.n.PatientId, x.a.VisitPatientName, x.a.StartAt, x.n.Status, x.n.FinalisedAt, x.n.Revision, x.n.UpdatedAt))
            .AsNoTracking()
            .ToListAsync(ct);
        return (items, total);
    }

    public void AddRevision(ClinicalNoteRevision revision) => db.ClinicalNoteRevisions.Add(revision);

    public async Task<IReadOnlyList<ClinicalNoteRevision>> ListRevisionsAsync(Guid noteId, CancellationToken ct) =>
        await db.ClinicalNoteRevisions.AsNoTracking().Where(r => r.NoteId == noteId).OrderBy(r => r.Revision).ToListAsync(ct);

    private IQueryable<ClinicalNote> WithDiagnoses() => db.ClinicalNotes.Include(n => n.Diagnoses.OrderBy(d => d.SortOrder));
}
