namespace TeleMed.Domain.Rules;

public sealed record SchedulePolicy(int SlotDurationMinutes, int BufferMinutes, int MaxPerDay, int AdvanceDays, TimeZoneInfo TimeZone);

public readonly record struct WorkingWindow(DayOfWeek DayOfWeek, int StartMinute, int EndMinute);

public readonly record struct BusyInterval(DateTimeOffset Start, DateTimeOffset End, bool IsBooking);

public readonly record struct PlannedSlot(DateTimeOffset StartAt, DateTimeOffset EndAt, bool Available);

public static class SlotPlanner
{
    public const int MinutesPerDay = 24 * 60;

    public static IReadOnlyList<PlannedSlot> Plan(
        SchedulePolicy policy,
        IReadOnlyCollection<WorkingWindow> hours,
        IReadOnlySet<DateOnly> holidays,
        IReadOnlyCollection<BusyInterval> busy,
        DateOnly from,
        DateOnly to,
        DateTimeOffset now)
    {
        var (first, last) = BookableRange(policy, from, to, now);
        var slots = new List<PlannedSlot>();
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            if (holidays.Contains(date))
            {
                continue;
            }

            var day = LayOutDay(policy, hours, date);
            if (day.Count == 0)
            {
                continue;
            }

            var full = busy.Count(b => b.IsBooking && LocalDate(b.Start, policy.TimeZone) == date) >= policy.MaxPerDay;
            foreach (var (start, end) in day)
            {
                var available = !full && start > now && !busy.Any(b => b.Start < end && start < b.End);
                slots.Add(new PlannedSlot(start, end, available));
            }
        }

        return slots;
    }

    public static (DateOnly From, DateOnly To) BookableRange(SchedulePolicy policy, DateOnly from, DateOnly to, DateTimeOffset now)
    {
        var today = LocalDate(now, policy.TimeZone);
        var lastBookable = today.AddDays(policy.AdvanceDays - 1);
        return (from > today ? from : today, to < lastBookable ? to : lastBookable);
    }

    public static bool IsWithinWorkingHours(TimeZoneInfo zone, IEnumerable<WorkingWindow> hours, DateTimeOffset start, DateTimeOffset end)
    {
        var localStart = TimeZoneInfo.ConvertTime(start, zone).DateTime;
        var date = DateOnly.FromDateTime(localStart);
        var midnight = date.ToDateTime(TimeOnly.MinValue);
        var startMinute = (localStart - midnight).TotalMinutes;
        var endMinute = (TimeZoneInfo.ConvertTime(end, zone).DateTime - midnight).TotalMinutes;
        return hours.Any(h => h.DayOfWeek == date.DayOfWeek && h.StartMinute <= startMinute && endMinute <= h.EndMinute);
    }

    public static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    // Starts are built from the wall clock (not by adding durations) so a window straddling a DST change keeps the doctor's clock.
    private static List<(DateTimeOffset Start, DateTimeOffset End)> LayOutDay(SchedulePolicy policy, IEnumerable<WorkingWindow> hours, DateOnly date)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue);
        var step = policy.SlotDurationMinutes + policy.BufferMinutes;
        var starts = new List<DateTimeOffset>();
        foreach (var window in hours.Where(h => h.DayOfWeek == date.DayOfWeek))
        {
            for (var minute = window.StartMinute; minute + policy.SlotDurationMinutes <= window.EndMinute; minute += step)
            {
                if (ToInstant(midnight.AddMinutes(minute), policy.TimeZone) is { } start)
                {
                    starts.Add(start);
                }
            }
        }

        starts.Sort();
        var duration = TimeSpan.FromMinutes(policy.SlotDurationMinutes);
        var day = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        foreach (var start in starts)
        {
            if (day.Count == policy.MaxPerDay)
            {
                break;
            }

            if (day.Count == 0 || start >= day[^1].End)
            {
                day.Add((start, start + duration));
            }
        }

        return day;
    }

    private static DateTimeOffset? ToInstant(DateTime local, TimeZoneInfo zone)
    {
        if (zone.IsInvalidTime(local))
        {
            return null;
        }

        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
