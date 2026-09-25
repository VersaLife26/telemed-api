using System.Globalization;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Notifications.Templates;

// One unambiguous day-month-year, 24h format for every locale: si/ta date idioms are not localised.
public static class NotificationFormat
{
    private static readonly TimeZoneInfo Colombo = IanaTimeZone.Find(PlatformPolicy.TimeZoneId);

    public static string DateTime(DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, Colombo).ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    public static string Money(long cents, string currency) =>
        (currency == PlatformPolicy.Currency ? "Rs. " : currency + " ") + (cents / 100m).ToString("#,##0.00", CultureInfo.InvariantCulture);
}
