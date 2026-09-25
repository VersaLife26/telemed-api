using FluentValidation;
using FluentValidation.Results;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Scheduling;

public sealed class SlotService(IDoctorRepository doctors, ISchedulingRepository scheduling, TimeProvider time)
{
    public async Task<SlotsDto> GetAsync(Guid doctorId, SlotQuery query, CancellationToken ct)
    {
        var doctor = await doctors.FindListedAsync(doctorId, ct) ?? throw new NotFoundException("Doctor not found.");
        var policy = doctor.ToPolicy();
        var now = time.GetUtcNow();
        var from = query.From ?? SlotPlanner.LocalDate(now, policy.TimeZone);
        var to = query.To ?? from.AddDays(ScheduleLimits.DefaultSlotRangeDays - 1);
        if (to < from || to.DayNumber - from.DayNumber >= ScheduleLimits.MaxSlotRangeDays)
        {
            throw new ValidationException([new ValidationFailure("to", $"The range must run forwards and cover at most {ScheduleLimits.MaxSlotRangeDays} days.")]);
        }

        var (first, last) = SlotPlanner.BookableRange(policy, from, to, now);
        if (first > last)
        {
            return new SlotsDto(doctor.TimeZone, []);
        }

        var hours = await scheduling.ListWorkingHoursAsync(doctor.Id, ct);
        var holidays = await scheduling.ListHolidaysAsync(doctor.Id, first, last, ct);
        // A calendar day in any zone lies within the UTC days either side of it.
        var busy = await scheduling.ListBusyAsync(
            doctor.Id,
            new DateTimeOffset(first.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(last.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            null,
            ct);
        var slots = SlotPlanner.Plan(
            policy,
            hours.Select(h => h.ToWindow()).ToList(),
            holidays.Select(h => h.Date).ToHashSet(),
            busy,
            first,
            last,
            now);
        return new SlotsDto(doctor.TimeZone, slots.Select(s => s.ToDto()).ToList());
    }
}
