using TeleMed.Application.Common;
using TeleMed.Application.Payouts;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Analytics;

public sealed class AnalyticsService(IAnalyticsRepository analytics, TimeProvider time)
{
    public async Task<DashboardDto> DashboardAsync(DateRangeQuery query, CancellationToken ct)
    {
        var range = Resolve(query);
        var bookings = await analytics.CountBookingsAsync(range.FromUtc, range.ToUtc, null, ct);
        var revenue = await analytics.SumRevenueAsync(range.FromUtc, range.ToUtc, null, ct);
        return new DashboardDto(
            range.From,
            range.To,
            bookings.Bookings,
            bookings.Completed,
            bookings.Cancelled,
            bookings.NoShows,
            revenue.GrossCents,
            revenue.CommissionCents,
            await analytics.CountActiveDoctorsAsync(ct),
            await analytics.CountNewPatientsAsync(range.FromUtc, range.ToUtc, ct),
            PlatformPolicy.Currency);
    }

    public Task<IReadOnlyList<RevenuePointDto>> RevenueAsync(RevenueQuery query, CancellationToken ct)
    {
        var range = Resolve(query);
        return analytics.RevenueSeriesAsync(range.FromUtc, range.ToUtc, PlatformPolicy.TimeZoneId, query.Granularity, ct);
    }

    public Task<IReadOnlyList<BookingsDayDto>> BookingsAsync(DateRangeQuery query, CancellationToken ct)
    {
        var range = Resolve(query);
        return analytics.BookingsSeriesAsync(range.FromUtc, range.ToUtc, PlatformPolicy.TimeZoneId, ct);
    }

    public Task<IReadOnlyList<TopDoctorDto>> TopDoctorsAsync(TopDoctorsQuery query, CancellationToken ct)
    {
        var range = Resolve(query);
        return analytics.TopDoctorsAsync(range.FromUtc, range.ToUtc, query.Limit, ct);
    }

    private LocalDateRange Resolve(IDateRange query) => LocalDateRange.Resolve(query.From, query.To, PayoutService.Zone, time.GetUtcNow());
}
