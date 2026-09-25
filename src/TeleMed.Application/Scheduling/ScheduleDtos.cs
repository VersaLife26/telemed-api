namespace TeleMed.Application.Scheduling;

public sealed record WorkingHourDto(int DayOfWeek, int StartMinute, int EndMinute);

public record ScheduleDto
{
    public int SlotDurationMinutes { get; init; }
    public int BufferMinutes { get; init; }
    public int MaxPerDay { get; init; }
    public int AdvanceDays { get; init; }
    public string Timezone { get; init; } = "";
    public IReadOnlyList<WorkingHourDto> WorkingHours { get; init; } = [];
}

public sealed record ScheduleUpdatedDto : ScheduleDto
{
    public ScheduleUpdatedDto()
    {
    }

    public ScheduleUpdatedDto(ScheduleDto schedule)
        : base(schedule)
    {
    }

    public int AppointmentsOutsideNewHours { get; init; }
}

public sealed record UpdateScheduleRequest(
    int SlotDurationMinutes,
    int BufferMinutes,
    int MaxPerDay,
    int AdvanceDays,
    string Timezone,
    IReadOnlyList<WorkingHourDto> WorkingHours);

public sealed record HolidayDto(Guid Id, Guid? DoctorId, DateOnly Date, string Reason, DateTimeOffset CreatedAt);

public sealed record CreateHolidayRequest(DateOnly Date, string Reason, bool CancelBooked = false);

public sealed record HolidayQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed record SlotBlockDto(Guid Id, Guid DoctorId, DateTimeOffset StartAt, DateTimeOffset EndAt, string Reason, Guid CreatedByAdminId, DateTimeOffset CreatedAt);

public sealed record CreateSlotBlockRequest(DateTimeOffset StartAt, DateTimeOffset EndAt, string Reason, bool CancelBooked = false);

public sealed record SlotBlockQuery
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}

public sealed record SlotQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed record SlotDto(DateTimeOffset StartAt, DateTimeOffset EndAt, bool Available);

public sealed record SlotsDto(string Timezone, IReadOnlyList<SlotDto> Slots);
