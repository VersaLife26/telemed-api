using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class SlotPlannerTests
{
    private static readonly TimeZoneInfo Colombo = IanaTimeZone.Find("Asia/Colombo");
    private static readonly TimeZoneInfo NewYork = IanaTimeZone.Find("America/New_York");

    // 2027-03-15 is a Monday.
    private static readonly DateOnly Monday = new(2027, 3, 15);
    private static readonly DateTimeOffset LongAgo = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static SchedulePolicy Policy(int duration = 15, int buffer = 5, int maxPerDay = 100, int advanceDays = 180, TimeZoneInfo? zone = null) =>
        new(duration, buffer, maxPerDay, advanceDays, zone ?? Colombo);

    private static WorkingWindow Window(DayOfWeek day, int startHour, int startMinute, int endHour, int endMinute) =>
        new(day, (startHour * 60) + startMinute, (endHour * 60) + endMinute);

    private static WorkingWindow[] EveryDay(int startHour, int endHour) =>
        Enum.GetValues<DayOfWeek>().Select(d => Window(d, startHour, 0, endHour, 0)).ToArray();

    private static DateTimeOffset At(DateOnly date, int hour, int minute, TimeZoneInfo? zone = null)
    {
        var local = date.ToDateTime(new TimeOnly(hour, minute));
        return new DateTimeOffset(local, (zone ?? Colombo).GetUtcOffset(local)).ToUniversalTime();
    }

    private static IReadOnlyList<PlannedSlot> PlanDay(
        SchedulePolicy policy,
        WorkingWindow[] hours,
        DateOnly date,
        BusyInterval[]? busy = null,
        DateTimeOffset? now = null) =>
        SlotPlanner.Plan(policy, hours, new HashSet<DateOnly>(), busy ?? [], date, date, now ?? LongAgo);

    private static string[] LocalStarts(IEnumerable<PlannedSlot> slots, TimeZoneInfo? zone = null) =>
        slots.Select(s => TimeZoneInfo.ConvertTime(s.StartAt, zone ?? Colombo).ToString("HH:mm")).ToArray();

    [Theory]
    [InlineData(15, 5, 9 * 60, 12 * 60, new[] { "09:00", "09:20", "09:40", "10:00", "10:20", "10:40", "11:00", "11:20", "11:40" })]
    [InlineData(30, 0, 9 * 60, 12 * 60, new[] { "09:00", "09:30", "10:00", "10:30", "11:00", "11:30" })]
    [InlineData(60, 10, 9 * 60, (11 * 60) + 30, new[] { "09:00", "10:10" })]
    [InlineData(30, 5, 9 * 60, (9 * 60) + 20, new string[0])]
    [InlineData(10, 50, 9 * 60, 12 * 60, new[] { "09:00", "10:00", "11:00" })]
    public void Lays_out_slots_by_duration_plus_buffer_and_drops_partial_slots(int duration, int buffer, int start, int end, string[] expected)
    {
        var slots = PlanDay(Policy(duration, buffer), [new(DayOfWeek.Monday, start, end)], Monday);

        LocalStarts(slots).ShouldBe(expected);
        slots.ShouldAllBe(s => s.EndAt - s.StartAt == TimeSpan.FromMinutes(duration));
        slots.ShouldAllBe(s => s.Available);
    }

    [Fact]
    public void Builds_instants_in_the_doctors_zone_not_utc()
    {
        var colombo = PlanDay(Policy(), [Window(DayOfWeek.Monday, 9, 0, 12, 0)], Monday);
        var dubai = PlanDay(Policy(zone: IanaTimeZone.Find("Asia/Dubai")), [Window(DayOfWeek.Monday, 9, 0, 12, 0)], Monday);

        colombo[0].StartAt.ShouldBe(new DateTimeOffset(2027, 3, 15, 3, 30, 0, TimeSpan.Zero));
        dubai[0].StartAt.ShouldBe(new DateTimeOffset(2027, 3, 15, 5, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_window_can_end_at_midnight()
    {
        var slots = PlanDay(Policy(30, 0), [new(DayOfWeek.Monday, 23 * 60, SlotPlanner.MinutesPerDay)], Monday);

        LocalStarts(slots).ShouldBe(["23:00", "23:30"]);
        slots[^1].EndAt.ShouldBe(At(Monday.AddDays(1), 0, 0));
    }

    [Fact]
    public void Overlapping_windows_never_produce_duplicate_or_overlapping_slots()
    {
        var aligned = PlanDay(Policy(30, 0), [Window(DayOfWeek.Monday, 9, 0, 10, 0), Window(DayOfWeek.Monday, 9, 30, 10, 30)], Monday);
        var offGrid = PlanDay(Policy(30, 0), [Window(DayOfWeek.Monday, 9, 0, 10, 0), Window(DayOfWeek.Monday, 9, 10, 10, 10)], Monday);

        LocalStarts(aligned).ShouldBe(["09:00", "09:30", "10:00"]);
        LocalStarts(offGrid).ShouldBe(["09:00", "09:30"]);
    }

    [Fact]
    public void Truncates_each_day_to_max_per_day()
    {
        var slots = PlanDay(Policy(maxPerDay: 10), [Window(DayOfWeek.Monday, 9, 0, 17, 0)], Monday);

        slots.Count.ShouldBe(10);
        LocalStarts(slots)[^1].ShouldBe("12:00");
    }

    [Fact]
    public void A_day_with_max_per_day_bookings_is_full()
    {
        var policy = Policy(30, 0, maxPerDay: 3);
        var hours = EveryDay(9, 12);
        BusyInterval Booking(int hour) => new(At(Monday, hour, 0), At(Monday, hour, 30), IsBooking: true);

        var twoBooked = PlanDay(policy, hours, Monday, [Booking(9), Booking(10)]);
        var threeBooked = SlotPlanner.Plan(policy, hours, new HashSet<DateOnly>(), [Booking(9), Booking(10), Booking(11)], Monday, Monday.AddDays(1), LongAgo);

        twoBooked.Where(s => s.Available).Select(s => TimeZoneInfo.ConvertTime(s.StartAt, Colombo).ToString("HH:mm")).ShouldBe(["09:30"]);
        threeBooked.Where(s => s.StartAt < At(Monday.AddDays(1), 0, 0)).ShouldAllBe(s => !s.Available);
        threeBooked.Where(s => s.StartAt >= At(Monday.AddDays(1), 0, 0)).ShouldAllBe(s => s.Available);
    }

    [Fact]
    public void Slot_blocks_do_not_count_toward_day_capacity()
    {
        var block = new BusyInterval(At(Monday, 9, 0), At(Monday, 9, 30), IsBooking: false);

        var slots = PlanDay(Policy(30, 0, maxPerDay: 1), EveryDay(9, 12), Monday, [block]);

        slots.Count.ShouldBe(1);
        slots[0].Available.ShouldBeFalse();
    }

    [Fact]
    public void Skips_wall_clock_times_that_fall_in_a_spring_forward_gap()
    {
        // 2027-03-14: New York jumps from 02:00 to 03:00.
        var date = new DateOnly(2027, 3, 14);

        var slots = PlanDay(Policy(60, 0, zone: NewYork), [Window(DayOfWeek.Sunday, 1, 0, 6, 0)], date);

        LocalStarts(slots, NewYork).ShouldBe(["01:00", "03:00", "04:00", "05:00"]);
        slots[0].StartAt.ShouldBe(new DateTimeOffset(2027, 3, 14, 6, 0, 0, TimeSpan.Zero));
        slots[1].StartAt.ShouldBe(new DateTimeOffset(2027, 3, 14, 7, 0, 0, TimeSpan.Zero));
        slots.ShouldAllBe(s => s.EndAt - s.StartAt == TimeSpan.FromHours(1));
        slots.Zip(slots.Skip(1)).ShouldAllBe(p => p.Second.StartAt >= p.First.EndAt);
    }

    [Fact]
    public void Takes_the_earlier_instant_for_ambiguous_fall_back_times()
    {
        // 2027-11-07: New York repeats 01:00-02:00.
        var date = new DateOnly(2027, 11, 7);

        var slots = PlanDay(Policy(30, 0, zone: NewYork), [Window(DayOfWeek.Sunday, 0, 0, 3, 0)], date, now: new DateTimeOffset(2027, 11, 1, 0, 0, 0, TimeSpan.Zero));

        slots.Select(s => s.StartAt.UtcDateTime.ToString("HH:mm")).ShouldBe(["04:00", "04:30", "05:00", "05:30", "07:00", "07:30"]);
        slots.Zip(slots.Skip(1)).ShouldAllBe(p => p.Second.StartAt >= p.First.EndAt);
    }

    [Fact]
    public void Holidays_remove_the_whole_day()
    {
        var holidays = new HashSet<DateOnly> { Monday };

        var slots = SlotPlanner.Plan(Policy(30, 0), EveryDay(9, 10), holidays, [], Monday, Monday.AddDays(1), LongAgo);

        slots.Select(s => SlotPlanner.LocalDate(s.StartAt, Colombo)).Distinct().ShouldBe([Monday.AddDays(1)]);
    }

    [Fact]
    public void Slots_that_have_started_are_unavailable()
    {
        var now = At(Monday, 10, 0);

        var slots = PlanDay(Policy(30, 0), EveryDay(9, 12), Monday, now: now);

        slots.Where(s => s.Available).Select(s => TimeZoneInfo.ConvertTime(s.StartAt, Colombo).ToString("HH:mm"))
            .ShouldBe(["10:30", "11:00", "11:30"]);
    }

    [Fact]
    public void Busy_time_blocks_every_slot_it_overlaps_even_off_grid()
    {
        var offGrid = new BusyInterval(At(Monday, 9, 50), At(Monday, 10, 10), IsBooking: true);
        var inBuffer = new BusyInterval(At(Monday, 9, 15), At(Monday, 9, 20), IsBooking: true);

        var slots = PlanDay(Policy(), [Window(DayOfWeek.Monday, 9, 0, 11, 0)], Monday, [offGrid, inBuffer]);

        slots.Where(s => !s.Available).Select(s => TimeZoneInfo.ConvertTime(s.StartAt, Colombo).ToString("HH:mm"))
            .ShouldBe(["09:40", "10:00"]);
    }

    [Fact]
    public void Clamps_the_range_to_today_through_advance_days()
    {
        var now = At(Monday, 8, 0);

        var slots = SlotPlanner.Plan(Policy(30, 0, advanceDays: 3), EveryDay(9, 10), new HashSet<DateOnly>(), [], Monday.AddDays(-5), Monday.AddDays(10), now);

        slots.Select(s => SlotPlanner.LocalDate(s.StartAt, Colombo)).Distinct().ShouldBe([Monday, Monday.AddDays(1), Monday.AddDays(2)]);
    }

    [Fact]
    public void Today_follows_the_doctors_zone()
    {
        // 20:00 UTC on the 14th is already 01:30 on the 15th in Colombo.
        var now = new DateTimeOffset(2027, 3, 14, 20, 0, 0, TimeSpan.Zero);

        SlotPlanner.BookableRange(Policy(advanceDays: 1), Monday.AddDays(-1), Monday.AddDays(5), now).ShouldBe((Monday, Monday));
    }

    [Fact]
    public void Checks_whether_a_booking_sits_inside_working_hours()
    {
        WorkingWindow[] hours = [Window(DayOfWeek.Monday, 9, 0, 12, 0), new(DayOfWeek.Monday, 23 * 60, SlotPlanner.MinutesPerDay)];

        SlotPlanner.IsWithinWorkingHours(Colombo, hours, At(Monday, 9, 0), At(Monday, 9, 15)).ShouldBeTrue();
        SlotPlanner.IsWithinWorkingHours(Colombo, hours, At(Monday, 11, 50), At(Monday, 12, 5)).ShouldBeFalse();
        SlotPlanner.IsWithinWorkingHours(Colombo, hours, At(Monday, 23, 30), At(Monday.AddDays(1), 0, 0)).ShouldBeTrue();
        SlotPlanner.IsWithinWorkingHours(Colombo, hours, At(Monday.AddDays(1), 9, 0), At(Monday.AddDays(1), 9, 15)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Asia/Colombo", true)]
    [InlineData("America/New_York", true)]
    [InlineData("Mars/Olympus", false)]
    [InlineData("", false)]
    public void Accepts_only_iana_zones(string id, bool valid)
    {
        IanaTimeZone.TryFind(id, out _).ShouldBe(valid);
    }
}
