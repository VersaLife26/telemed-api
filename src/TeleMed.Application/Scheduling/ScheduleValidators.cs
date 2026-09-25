using FluentValidation;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Scheduling;

public sealed class UpdateScheduleRequestValidator : AbstractValidator<UpdateScheduleRequest>
{
    private const int MaxWindows = 50;

    public UpdateScheduleRequestValidator()
    {
        RuleFor(x => x.SlotDurationMinutes).InclusiveBetween(5, 240);
        RuleFor(x => x.BufferMinutes).InclusiveBetween(0, 120);
        RuleFor(x => x.MaxPerDay).InclusiveBetween(1, 200);
        RuleFor(x => x.AdvanceDays).InclusiveBetween(1, ScheduleLimits.MaxAdvanceDays);
        RuleFor(x => x.Timezone).Must(tz => IanaTimeZone.TryFind(tz, out _)).WithMessage("Timezone must be an IANA time zone such as Asia/Colombo.");
        RuleFor(x => x.WorkingHours).NotNull().Must(h => h is null || h.Count <= MaxWindows).WithMessage($"At most {MaxWindows} working-hour windows.");
        RuleForEach(x => x.WorkingHours).ChildRules(h =>
        {
            h.RuleFor(x => x.DayOfWeek).InclusiveBetween(0, 6);
            h.RuleFor(x => x.StartMinute).InclusiveBetween(0, SlotPlanner.MinutesPerDay - 1);
            h.RuleFor(x => x.EndMinute).InclusiveBetween(1, SlotPlanner.MinutesPerDay).GreaterThan(x => x.StartMinute);
        });
        RuleFor(x => x.WorkingHours).Must(NotOverlap).When(x => x.WorkingHours is not null)
            .WithMessage("Working-hour windows on the same day must not overlap.");
    }

    private static bool NotOverlap(IReadOnlyList<WorkingHourDto> hours) =>
        hours.GroupBy(h => h.DayOfWeek).All(day =>
        {
            var ordered = day.OrderBy(h => h.StartMinute).ToList();
            return ordered.Zip(ordered.Skip(1)).All(pair => pair.Second.StartMinute >= pair.First.EndMinute);
        });
}

public sealed class CreateHolidayRequestValidator : AbstractValidator<CreateHolidayRequest>
{
    public CreateHolidayRequestValidator()
    {
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class HolidayQueryValidator : AbstractValidator<HolidayQuery>
{
    public HolidayQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null);
    }
}

public sealed class CreateSlotBlockRequestValidator : AbstractValidator<CreateSlotBlockRequest>
{
    public CreateSlotBlockRequestValidator()
    {
        RuleFor(x => x.StartAt).NotEmpty();
        RuleFor(x => x.EndAt).GreaterThan(x => x.StartAt);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class SlotBlockQueryValidator : AbstractValidator<SlotBlockQuery>
{
    public SlotBlockQueryValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From).When(x => x.From is not null && x.To is not null);
    }
}

public sealed class SlotQueryValidator : AbstractValidator<SlotQuery>
{
    public SlotQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null);
    }
}

internal static class ScheduleLimits
{
    public const int MaxAdvanceDays = 180;
    public const int DefaultSlotRangeDays = 14;
    public const int MaxSlotRangeDays = 31;
}
