using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Consultations;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class ConsultationRepository(AppDbContext db) : IConsultationRepository
{
    public void Add(Consultation consultation) => db.Consultations.Add(consultation);

    public Task<Consultation?> FindAsync(Guid id, CancellationToken ct) => db.Consultations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);

    public Task<Consultation?> FindByAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        db.Consultations.AsNoTracking().SingleOrDefaultAsync(c => c.AppointmentId == appointmentId, ct);

    public Task<Consultation?> FindForUpdateAsync(Guid id, CancellationToken ct) => db.Consultations.SingleOrDefaultAsync(c => c.Id == id, ct);

    public Task<Consultation?> FindForUpdateByAppointmentAsync(Guid appointmentId, CancellationToken ct) =>
        db.Consultations.SingleOrDefaultAsync(c => c.AppointmentId == appointmentId, ct);

    public async Task<Consultation> LockOrCreateAsync(Guid appointmentId, DateTimeOffset now, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync(
            $"""
             INSERT INTO consultations (id, appointment_id, status, created_at, updated_at)
             VALUES ({Guid.CreateVersion7()}, {appointmentId}, 'scheduled', {now}, {now})
             ON CONFLICT (appointment_id) DO NOTHING
             """,
            ct);
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM consultations WHERE appointment_id = {appointmentId} FOR UPDATE", ct);
        if (db.Consultations.Local.FirstOrDefault(c => c.AppointmentId == appointmentId) is { } tracked)
        {
            await db.Entry(tracked).ReloadAsync(ct);
            return tracked;
        }

        return await db.Consultations.SingleAsync(c => c.AppointmentId == appointmentId, ct);
    }

    public void AddEvent(ConsultationEvent consultationEvent) => db.ConsultationEvents.Add(consultationEvent);

    public async Task<IReadOnlyList<string?>> ListRecentEventDataAsync(
        Guid consultationId, ConsultationEventKind kind, UserRole role, int take, CancellationToken ct) =>
        await db.ConsultationEvents.AsNoTracking()
            .Where(e => e.ConsultationId == consultationId && e.Kind == kind && e.ActorRole == role)
            .OrderByDescending(e => e.Id)
            .Take(take)
            .Select(e => e.Data)
            .ToListAsync(ct);

    public void AddMessage(ConsultationMessage message) => db.ConsultationMessages.Add(message);

    public async Task<(IReadOnlyList<ConsultationMessage> Items, long Total)> ListMessagesAsync(Guid consultationId, int skip, int take, CancellationToken ct)
    {
        var query = db.ConsultationMessages.AsNoTracking().Where(m => m.ConsultationId == consultationId);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public Task<int> CountAheadAsync(Guid doctorId, Guid consultationId, DateTimeOffset startAt, CancellationToken ct) =>
        (from c in db.Consultations
         join a in db.Appointments on c.AppointmentId equals a.Id
         where a.DoctorId == doctorId
             && c.Id != consultationId
             && (c.Status == ConsultationStatus.Active || (c.Status == ConsultationStatus.Waiting && a.StartAt < startAt))
         select c.Id)
        .CountAsync(ct);

    public async Task<IReadOnlyList<int>> ListRecentDurationsAsync(Guid doctorId, int take, CancellationToken ct) =>
        await (from c in db.Consultations
               join a in db.Appointments on c.AppointmentId equals a.Id
               where a.DoctorId == doctorId && !a.IsTest && c.DurationSeconds != null
               orderby c.EndedAt descending
               select c.DurationSeconds!.Value)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UnattendedAppointment>> ListUnattendedEndedAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await (from a in db.Appointments
               join joined in db.Consultations on a.Id equals joined.AppointmentId into matches
               from c in matches.DefaultIfEmpty()
               where a.Status == AppointmentStatus.Confirmed
                   && a.EndAt <= now
                   && (c == null || c.DoctorJoinedAt == null || c.PatientJoinedAt == null)
               orderby a.EndAt
               select new UnattendedAppointment(a.Id, a.DoctorId))
            .AsNoTracking()
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OverrunConsultation>> ListOverrunActiveAsync(DateTimeOffset now, int limit, CancellationToken ct) =>
        await (from c in db.Consultations
               join a in db.Appointments on c.AppointmentId equals a.Id
               where c.Status == ConsultationStatus.Active && c.RunningLateNotifiedAt == null && a.EndAt < now
               orderby a.EndAt
               select new OverrunConsultation(c.Id, a.Id, a.DoctorId, a.StartAt))
            .AsNoTracking()
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<IdleConsultation>> ListIdleActiveAsync(DateTimeOffset cutoff, int limit, CancellationToken ct) =>
        await db.Consultations.AsNoTracking()
            .Where(c => c.Status == ConsultationStatus.Active
                && (c.StartedAt ?? c.UpdatedAt) < cutoff
                && !db.ConsultationEvents.Any(e => e.ConsultationId == c.Id
                    && (e.Kind == ConsultationEventKind.Joined || e.Kind == ConsultationEventKind.Left)
                    && e.CreatedAt >= cutoff))
            .OrderBy(c => c.StartedAt)
            .Take(limit)
            .Select(c => new IdleConsultation(c.Id, c.AppointmentId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<IdleConsultation>> ListStuckPendingAsync(DateTimeOffset now, int limit, CancellationToken ct)
    {
        var endedBefore = now - PlatformPolicy.DoctorJoinGraceAfterEnd;
        return await (from c in db.Consultations
                      join a in db.Appointments on c.AppointmentId equals a.Id
                      where (c.Status == ConsultationStatus.Scheduled || c.Status == ConsultationStatus.Waiting)
                          && (a.Status != AppointmentStatus.Confirmed || a.EndAt <= endedBefore)
                      orderby a.EndAt
                      select new IdleConsultation(c.Id, a.Id))
            .AsNoTracking()
            .Take(limit)
            .ToListAsync(ct);
    }
}
