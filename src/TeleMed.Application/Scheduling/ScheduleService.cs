using TeleMed.Application.Abstractions;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Scheduling;

public sealed class ScheduleService(
    ICurrentActor actor,
    IDoctorRepository doctors,
    ISchedulingRepository scheduling,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public async Task<ScheduleDto> GetMineAsync(CancellationToken ct) => await GetAsync(await doctors.MineAsync(actor, ct), ct);

    public async Task<ScheduleUpdatedDto> UpdateMineAsync(UpdateScheduleRequest request, CancellationToken ct) =>
        await UpdateAsync(await doctors.MineWritableAsync(actor, ct), request, ct);

    public async Task<ScheduleDto> GetAsync(Guid doctorId, CancellationToken ct) => await GetAsync(await doctors.RequireAsync(doctorId, ct), ct);

    public async Task<ScheduleUpdatedDto> UpdateAsync(Guid doctorId, UpdateScheduleRequest request, CancellationToken ct) =>
        await UpdateAsync(await doctors.RequireAsync(doctorId, ct), request, ct);

    private async Task<ScheduleDto> GetAsync(Doctor doctor, CancellationToken ct) =>
        doctor.ToScheduleDto(await scheduling.ListWorkingHoursAsync(doctor.Id, ct));

    private async Task<ScheduleUpdatedDto> UpdateAsync(Doctor doctor, UpdateScheduleRequest request, CancellationToken ct)
    {
        var existing = await scheduling.ListWorkingHoursAsync(doctor.Id, ct);
        var requested = request.WorkingHours.Select(h => new WorkingHour
        {
            DoctorId = doctor.Id,
            DayOfWeek = (DayOfWeek)h.DayOfWeek,
            StartMinute = h.StartMinute,
            EndMinute = h.EndMinute,
        }).ToList();
        var kept = existing.Where(e => requested.Any(r => SameWindow(r, e))).ToList();
        var added = requested.Where(r => !kept.Any(k => SameWindow(r, k))).ToList();
        scheduling.RemoveWorkingHours(existing.Except(kept));
        scheduling.AddWorkingHours(added);

        doctor.SlotDurationMinutes = request.SlotDurationMinutes;
        doctor.BufferMinutes = request.BufferMinutes;
        doctor.MaxPerDay = request.MaxPerDay;
        doctor.AdvanceDays = request.AdvanceDays;
        doctor.TimeZone = IanaTimeZone.Find(request.Timezone).Id;
        await unitOfWork.SaveChangesAsync(ct);

        var hours = kept.Concat(added).ToList();
        return new ScheduleUpdatedDto(doctor.ToScheduleDto(hours))
        {
            AppointmentsOutsideNewHours = await CountBookingsOutsideAsync(doctor, hours, ct),
        };
    }

    private async Task<int> CountBookingsOutsideAsync(Doctor doctor, IReadOnlyList<WorkingHour> hours, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var zone = IanaTimeZone.Find(doctor.TimeZone);
        var windows = hours.Select(h => h.ToWindow()).ToList();
        var busy = await scheduling.ListBusyAsync(doctor.Id, now, now.AddDays(ScheduleLimits.MaxAdvanceDays + 1), null, ct);
        return busy.Count(b => b.IsBooking && !SlotPlanner.IsWithinWorkingHours(zone, windows, b.Start, b.End));
    }

    private static bool SameWindow(WorkingHour a, WorkingHour b) =>
        a.DayOfWeek == b.DayOfWeek && a.StartMinute == b.StartMinute && a.EndMinute == b.EndMinute;
}
