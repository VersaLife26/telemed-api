using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Scheduling;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class SchedulingRepository(AppDbContext db) : ISchedulingRepository
{
    public async Task<IReadOnlyList<WorkingHour>> ListWorkingHoursAsync(Guid doctorId, CancellationToken ct) =>
        await db.WorkingHours.Where(h => h.DoctorId == doctorId).OrderBy(h => h.DayOfWeek).ThenBy(h => h.StartMinute).ToListAsync(ct);

    public void AddWorkingHours(IEnumerable<WorkingHour> hours) => db.WorkingHours.AddRange(hours);

    public void RemoveWorkingHours(IEnumerable<WorkingHour> hours) => db.WorkingHours.RemoveRange(hours);

    public Task<Holiday?> FindHolidayAsync(Guid id, CancellationToken ct) => db.Holidays.SingleOrDefaultAsync(h => h.Id == id, ct);

    public async Task<IReadOnlyList<Holiday>> ListHolidaysAsync(Guid? doctorId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var query = db.Holidays.AsNoTracking().Where(h => h.DoctorId == null || h.DoctorId == doctorId);
        if (from is { } f)
        {
            query = query.Where(h => h.Date >= f);
        }

        if (to is { } t)
        {
            query = query.Where(h => h.Date <= t);
        }

        return await query.OrderBy(h => h.Date).ThenBy(h => h.DoctorId).ToListAsync(ct);
    }

    public void AddHoliday(Holiday holiday) => db.Holidays.Add(holiday);

    public void RemoveHoliday(Holiday holiday) => db.Holidays.Remove(holiday);

    public Task<SlotBlock?> FindSlotBlockAsync(Guid id, CancellationToken ct) => db.SlotBlocks.SingleOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IReadOnlyList<SlotBlock>> ListSlotBlocksAsync(Guid doctorId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var query = db.SlotBlocks.AsNoTracking().Where(b => b.DoctorId == doctorId);
        if (from is { } f)
        {
            query = query.Where(b => b.EndAt > f);
        }

        if (to is { } t)
        {
            query = query.Where(b => b.StartAt < t);
        }

        return await query.OrderBy(b => b.StartAt).ThenBy(b => b.Id).ToListAsync(ct);
    }

    public void AddSlotBlock(SlotBlock block) => db.SlotBlocks.Add(block);

    public void RemoveSlotBlock(SlotBlock block) => db.SlotBlocks.Remove(block);

    public async Task<IReadOnlyList<BusyInterval>> ListBusyAsync(
        Guid doctorId, DateTimeOffset from, DateTimeOffset to, Guid? exceptAppointmentId, CancellationToken ct)
    {
        var blocks = await db.SlotBlocks.AsNoTracking()
            .Where(b => b.DoctorId == doctorId && b.StartAt < to && b.EndAt > from)
            .Select(b => new BusyInterval(b.StartAt, b.EndAt, false))
            .ToListAsync(ct);
        var bookings = await db.Appointments.AsNoTracking()
            .Where(a => a.DoctorId == doctorId && !a.IsTest && a.Status != AppointmentStatus.Cancelled && a.StartAt < to && a.EndAt > from
                && a.Id != exceptAppointmentId)
            .Select(a => new BusyInterval(a.StartAt, a.EndAt, true))
            .ToListAsync(ct);
        var holds = await db.RescheduleRequests.AsNoTracking()
            .Where(r => r.DoctorId == doctorId && r.Status == RescheduleStatus.Pending && r.ProposedStartAt < to && r.ProposedEndAt > from
                && r.AppointmentId != exceptAppointmentId)
            .Select(r => new BusyInterval(r.ProposedStartAt, r.ProposedEndAt, false))
            .ToListAsync(ct);
        return [.. blocks, .. bookings, .. holds];
    }

    public async Task<IReadOnlyList<Guid>> ListBookedDoctorIdsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        await db.Appointments
            .Where(a => !a.IsTest
                && (a.Status == AppointmentStatus.PendingPayment || a.Status == AppointmentStatus.Confirmed)
                && a.StartAt < to && a.EndAt > from)
            .Select(a => a.DoctorId)
            .Union(db.RescheduleRequests
                .Where(r => r.Status == RescheduleStatus.Pending && r.ProposedStartAt < to && r.ProposedEndAt > from)
                .Select(r => r.DoctorId))
            .OrderBy(id => id)
            .ToListAsync(ct);
}
