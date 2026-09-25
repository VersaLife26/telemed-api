using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Scheduling;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class ScheduleMapper
{
    public static partial HolidayDto ToDto(this Holiday holiday);

    public static partial SlotBlockDto ToDto(this SlotBlock block);

    public static WorkingHourDto ToDto(this WorkingHour hour) => new((int)hour.DayOfWeek, hour.StartMinute, hour.EndMinute);

    public static WorkingWindow ToWindow(this WorkingHour hour) => new(hour.DayOfWeek, hour.StartMinute, hour.EndMinute);

    public static SlotDto ToDto(this PlannedSlot slot) => new(slot.StartAt, slot.EndAt, slot.Available);

    public static SchedulePolicy ToPolicy(this Doctor doctor) =>
        new(doctor.SlotDurationMinutes, doctor.BufferMinutes, doctor.MaxPerDay, doctor.AdvanceDays, IanaTimeZone.Find(doctor.TimeZone));

    public static ScheduleDto ToScheduleDto(this Doctor doctor, IEnumerable<WorkingHour> hours) => new()
    {
        SlotDurationMinutes = doctor.SlotDurationMinutes,
        BufferMinutes = doctor.BufferMinutes,
        MaxPerDay = doctor.MaxPerDay,
        AdvanceDays = doctor.AdvanceDays,
        Timezone = doctor.TimeZone,
        WorkingHours = hours.OrderBy(h => h.DayOfWeek).ThenBy(h => h.StartMinute).Select(h => h.ToDto()).ToList(),
    };
}
