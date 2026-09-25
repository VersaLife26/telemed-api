using TeleMed.Application.Payouts;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Jobs;

// Ticks hourly; the first tick after 02:00 local builds yesterday's batch and later ticks find it already built.
public sealed class DailyPayoutsJob(PayoutService payouts, TimeProvider time) : IBackgroundJob
{
    public async Task RunAsync(CancellationToken ct)
    {
        var local = TimeZoneInfo.ConvertTime(time.GetUtcNow(), PayoutService.Zone);
        if (local.Hour < PlatformPolicy.DailyPayoutsLocalHour)
        {
            return;
        }

        await payouts.RunAsync(DateOnly.FromDateTime(local.DateTime).AddDays(-1), adminId: null, ct);
    }
}
