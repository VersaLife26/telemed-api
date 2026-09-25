using FluentValidation;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Common;

// Inclusive local dates, resolved to a half-open UTC interval in the given zone.
public sealed record LocalDateRange(DateOnly From, DateOnly To, DateTimeOffset FromUtc, DateTimeOffset ToUtc, TimeZoneInfo Zone)
{
    public const int DefaultDays = 30;
    public const int MaxDays = 366;

    public static LocalDateRange Resolve(DateOnly? from, DateOnly? to, TimeZoneInfo zone, DateTimeOffset now)
    {
        var end = to ?? IanaTimeZone.Today(now, zone);
        var start = from ?? end.AddDays(1 - DefaultDays);
        return new LocalDateRange(start, end, IanaTimeZone.StartOfDay(start, zone), IanaTimeZone.StartOfDay(end.AddDays(1), zone), zone);
    }
}

public interface IDateRange
{
    DateOnly? From { get; }
    DateOnly? To { get; }
}

public sealed record DateRangeQuery : IDateRange
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class DateRangeValidator : AbstractValidator<IDateRange>
{
    public DateRangeValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null);
        RuleFor(x => x.To)
            .Must((x, to) => to!.Value.DayNumber - x.From!.Value.DayNumber < LocalDateRange.MaxDays)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage($"The range can span at most {LocalDateRange.MaxDays} days.");
    }
}

public sealed class DateRangeQueryValidator : AbstractValidator<DateRangeQuery>
{
    public DateRangeQueryValidator()
    {
        Include(new DateRangeValidator());
    }
}
