using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Prescriptions;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class PrescriptionRepository(AppDbContext db) : IPrescriptionRepository
{
    public void Add(Prescription prescription) => db.Prescriptions.Add(prescription);

    public Task<Prescription?> FindAsync(Guid id, CancellationToken ct) => WithItems().AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);

    public Task<Prescription?> FindForUpdateAsync(Guid id, CancellationToken ct) => WithItems().SingleOrDefaultAsync(p => p.Id == id, ct);

    public Task<Prescription?> FindByAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        WithItems().AsNoTracking().SingleOrDefaultAsync(p => p.AppointmentId == appointmentId, ct);

    public async Task<(IReadOnlyList<Prescription> Items, long Total)> ListAsync(
        Guid? patientId, Guid? doctorId, int skip, int take, CancellationToken ct)
    {
        var query = db.Prescriptions.AsNoTracking();
        if (patientId is { } patient)
        {
            query = query.Where(p => p.PatientId == patient);
        }

        if (doctorId is { } doctor)
        {
            query = query.Where(p => p.DoctorId == doctor);
        }

        var total = await query.LongCountAsync(ct);
        var items = await query
            .Include(p => p.Items.OrderBy(i => i.SortOrder))
            .OrderByDescending(p => p.IssuedAt).ThenBy(p => p.Id)
            .Skip(skip).Take(take)
            .AsSplitQuery()
            .ToListAsync(ct);
        return (items, total);
    }

    private IQueryable<Prescription> WithItems() => db.Prescriptions.Include(p => p.Items.OrderBy(i => i.SortOrder));
}
