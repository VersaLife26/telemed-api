using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class PlatformPolicyTests
{
    [Fact]
    public void Platform_time_zone_resolves()
    {
        TimeZoneInfo.FindSystemTimeZoneById(PlatformPolicy.TimeZoneId).BaseUtcOffset.ShouldBe(TimeSpan.FromMinutes(330));
    }

    [Fact]
    public void Notification_backoff_increases()
    {
        PlatformPolicy.NotificationBackoff.ShouldBeInOrder(SortDirection.Ascending);
    }
}
