using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Appointments;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class AppointmentRepository(AppDbContext db) : IAppointmentRepository
{
    public void Add(Appointment appointment) => db.Appointments.Add(appointment);

    public Task<Appointment?> FindAsync(Guid id, CancellationToken ct) => db.Appointments.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);

    public Task<Appointment?> FindForUpdateAsync(Guid id, CancellationToken ct) => db.Appointments.SingleOrDefaultAsync(a => a.Id == id, ct);

    public async Task<(IReadOnlyList<Appointment> Items, long Total)> ListAsync(AppointmentFilter filter, CancellationToken ct)
    {
        var query = db.Appointments.AsNoTracking();
        if (!filter.IncludeTest)
        {
            query = query.Where(a => !a.IsTest);
        }

        if (filter.PatientId is { } patientId)
        {
            query = query.Where(a => a.PatientId == patientId);
        }

        if (filter.DoctorId is { } doctorId)
        {
            query = query.Where(a => a.DoctorId == doctorId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(a => a.Status == status);
        }

        if (filter.From is { } from)
        {
            query = query.Where(a => a.EndAt > from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(a => a.StartAt < to);
        }

        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(a => a.StartAt).ThenBy(a => a.Id).Skip(filter.Skip).Take(filter.Take).ToListAsync(ct);
        return (items, total);
    }

    // A visit booked for someone else carries a visit relation; only the patient's own visits describe the patient.
    public Task<Appointment?> FindLastSelfVisitAsync(Guid patientId, CancellationToken ct) =>
        db.Appointments
            .FromSql($"SELECT *, xmin FROM appointments WHERE patient_id = {patientId} AND NOT is_test AND COALESCE(trim(intake->>'visitRelation'), '') = ''")
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<bool> PatientHasLiveOverlapAsync(Guid patientId, DateTimeOffset start, DateTimeOffset end, Guid? exceptAppointmentId, CancellationToken ct) =>
        db.Appointments.AnyAsync(
            a => a.PatientId == patientId && !a.IsTest && a.Status != AppointmentStatus.Cancelled && a.StartAt < end && a.EndAt > start
                && a.Id != exceptAppointmentId,
            ct);

    public async Task<IReadOnlyList<Appointment>> ListLiveForUpdateAsync(
        IReadOnlyCollection<Guid> doctorIds, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        await db.Appointments
            .Where(a => doctorIds.Contains(a.DoctorId)
                && !a.IsTest
                && (a.Status == AppointmentStatus.PendingPayment || a.Status == AppointmentStatus.Confirmed)
                && a.StartAt < to && a.EndAt > from)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListLiveDoctorIdsForPatientAsync(Guid patientId, DateTimeOffset after, CancellationToken ct) =>
        await LiveForPatient(patientId, after).AsNoTracking().Select(a => a.DoctorId).Distinct().ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> ListLiveForPatientForUpdateAsync(Guid patientId, DateTimeOffset after, CancellationToken ct) =>
        await LiveForPatient(patientId, after).ToListAsync(ct);

    private IQueryable<Appointment> LiveForPatient(Guid patientId, DateTimeOffset after) =>
        db.Appointments.Where(a => a.PatientId == patientId
            && !a.IsTest
            && (a.Status == AppointmentStatus.PendingPayment || a.Status == AppointmentStatus.Confirmed)
            && a.StartAt > after);

    public async Task<IReadOnlyList<ExpiredBooking>> ListExpiredUnpaidAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.PendingPayment && a.PaymentDueAt <= now)
            .OrderBy(a => a.PaymentDueAt)
            .Take(limit)
            .Select(a => new ExpiredBooking(a.Id, a.DoctorId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ReminderCandidate>> ListReminderCandidatesAsync(DateTimeOffset after, DateTimeOffset until, CancellationToken ct) =>
        await (from a in db.Appointments
               join d in db.Doctors on a.DoctorId equals d.Id
               where a.Status == AppointmentStatus.Confirmed && !a.IsTest && a.StartAt > after && a.StartAt <= until
               orderby a.StartAt
               select new ReminderCandidate(a.Id, a.PatientId, d.DisplayName, a.StartAt, a.ConfirmedAt))
            .AsNoTracking()
            .ToListAsync(ct);

    public Task<Appointment?> FindNextConfirmedAsync(
        Guid doctorId, Guid exceptAppointmentId, DateTimeOffset startsAfter, DateTimeOffset startsBefore, CancellationToken ct) =>
        db.Appointments.AsNoTracking()
            .Where(a => a.DoctorId == doctorId
                && a.Id != exceptAppointmentId
                && a.Status == AppointmentStatus.Confirmed
                && a.StartAt > startsAfter
                && a.StartAt <= startsBefore)
            .OrderBy(a => a.StartAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListConfirmedEndedBeforeAsync(DateTimeOffset cutoff, int limit, CancellationToken ct) =>
        await db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Confirmed && a.EndAt < cutoff)
            .OrderBy(a => a.EndAt)
            .Take(limit)
            .Select(a => a.Id)
            .ToListAsync(ct);
}
