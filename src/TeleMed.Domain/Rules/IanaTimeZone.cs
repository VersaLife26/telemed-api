using System.Diagnostics.CodeAnalysis;

namespace TeleMed.Domain.Rules;

public static class IanaTimeZone
{
    public static bool TryFind(string? id, [NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        zone = null;
        return !string.IsNullOrWhiteSpace(id)
            && TimeZoneInfo.TryFindSystemTimeZoneById(id, out zone)
            && zone.HasIanaId;
    }

    public static TimeZoneInfo Find(string id) =>
        TryFind(id, out var zone) ? zone : throw new InvalidTimeZoneException($"'{id}' is not an IANA time zone.");

    public static DateOnly Today(DateTimeOffset now, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);

    // Midnight can fall in a DST gap in some zones; the day then starts at the first valid wall-clock time.
    public static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
