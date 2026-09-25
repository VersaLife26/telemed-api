using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class TreatingAccessTests
{
    private static readonly DateTimeOffset Started = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Ended = Started.AddMinutes(20);
    private static readonly TimeSpan Window = TimeSpan.FromDays(30) + TimeSpan.FromMinutes(5);

    [Fact]
    public void The_window_is_thirty_days_and_five_minutes()
    {
        PlatformPolicy.TreatingAccessWindow.ShouldBe(Window);
    }

    [Fact]
    public void Access_runs_from_the_start_until_the_window_after_the_end_closes()
    {
        TreatingAccess.Grants(Started, null, Started).ShouldBeTrue();
        TreatingAccess.Grants(Started, Ended, Ended + Window - TimeSpan.FromTicks(1)).ShouldBeTrue();
        TreatingAccess.Grants(Started, Ended, Ended + Window).ShouldBeFalse();
        TreatingAccess.ExpiresAt(Started, Ended).ShouldBe(Ended + Window);
    }

    [Fact]
    public void A_consultation_that_never_ended_counts_from_its_start()
    {
        TreatingAccess.Grants(Started, null, Started + Window - TimeSpan.FromTicks(1)).ShouldBeTrue();
        TreatingAccess.Grants(Started, null, Started + Window).ShouldBeFalse();
    }

    [Fact]
    public void A_consultation_that_never_started_grants_nothing()
    {
        TreatingAccess.Grants(null, null, Started).ShouldBeFalse();
        TreatingAccess.Grants(null, Ended, Ended).ShouldBeFalse();
        TreatingAccess.ExpiresAt(null, Ended).ShouldBeNull();
    }

    [Fact]
    public void The_cutoff_matches_the_grant()
    {
        var now = Ended + Window;

        TreatingAccess.LastSeenCutoff(now).ShouldBe(Ended);
        TreatingAccess.Grants(Started, Ended.AddTicks(1), now).ShouldBeTrue();
    }
}
