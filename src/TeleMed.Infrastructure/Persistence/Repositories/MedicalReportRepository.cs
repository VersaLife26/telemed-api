using Microsoft.EntityFrameworkCore;
using TeleMed.Application.MedicalReports;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class MedicalReportRepository(AppDbContext db) : IMedicalReportRepository
{
    public void Add(MedicalReport report) => db.MedicalReports.Add(report);

    public Task<MedicalReport?> FindAsync(Guid id, CancellationToken ct) =>
        db.MedicalReports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);

    public Task<MedicalReport?> FindForUpdateAsync(Guid id, CancellationToken ct) =>
        db.MedicalReports.SingleOrDefaultAsync(r => r.Id == id, ct);

    public Task<MedicalReport?> FindByAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        db.MedicalReports.AsNoTracking().SingleOrDefaultAsync(r => r.AppointmentId == appointmentId, ct);

    public async Task<(IReadOnlyList<MedicalReport> Items, long Total)> ListAsync(
        Guid? patientId, Guid? doctorId, int skip, int take, CancellationToken ct)
    {
        var query = db.MedicalReports.AsNoTracking();
        if (patientId is { } patient)
        {
            query = query.Where(r => r.PatientId == patient);
        }

        if (doctorId is { } doctor)
        {
            query = query.Where(r => r.DoctorId == doctor);
        }

        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.IssuedAt).ThenBy(r => r.Id)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return (items, total);
    }
}
