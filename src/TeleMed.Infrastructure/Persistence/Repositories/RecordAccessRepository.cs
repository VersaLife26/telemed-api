using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Vault;
using TeleMed.Domain.Entities;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class RecordAccessRepository(AppDbContext db) : IRecordAccessRepository
{
    public void Add(RecordAccessLog log) => db.RecordAccessLogs.Add(log);

    public Task<TreatingConsultation?> FindLatestTreatingAsync(Guid doctorId, Guid patientId, CancellationToken ct) =>
        (from c in db.Consultations
         join a in db.Appointments on c.AppointmentId equals a.Id
         where a.DoctorId == doctorId && a.PatientId == patientId && c.StartedAt != null
         orderby (c.EndedAt ?? c.StartedAt) descending
         select new TreatingConsultation(c.StartedAt!.Value, c.EndedAt))
        .AsNoTracking()
        .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TreatedPatient>> ListTreatedPatientsAsync(Guid doctorId, DateTimeOffset lastSeenAfter, CancellationToken ct) =>
        await (from c in db.Consultations
               join a in db.Appointments on c.AppointmentId equals a.Id
               join u in db.Users on a.PatientId equals u.Id
               where a.DoctorId == doctorId && c.StartedAt != null
               group (c.EndedAt ?? c.StartedAt!.Value) by new { u.Id, u.FullName } into g
               where g.Max() > lastSeenAfter
               orderby g.Max() descending
               select new TreatedPatient(g.Key.Id, g.Key.FullName, g.Max()))
            .AsNoTracking()
            .ToListAsync(ct);
}
