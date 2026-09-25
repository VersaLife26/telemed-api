using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Application.Doctors;
using TeleMed.Application.Scheduling;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Analytics;

// The signed-in doctor's own numbers; dates are in the doctor's time zone.
public sealed class DoctorInsightsService(ICurrentActor actor, IDoctorRepository doctors, IAnalyticsRepository analytics, TimeProvider time)
{
    public async Task<DoctorAnalyticsDto> AnalyticsAsync(DateRangeQuery query, CancellationToken ct)
    {
        var (doctor, range) = await ResolveAsync(query, ct);
        var bookings = await analytics.CountBookingsAsync(range.FromUtc, range.ToUtc, doctor.Id, ct);
        var revenue = await analytics.SumRevenueAsync(range.FromUtc, range.ToUtc, doctor.Id, ct);
        return new DoctorAnalyticsDto(
            range.From,
            range.To,
            bookings.Bookings,
            bookings.Completed,
            bookings.NoShows,
            bookings.Cancelled,
            Rate(bookings.Completed, bookings.Bookings),
            Rate(bookings.NoShows, bookings.Bookings),
            revenue.GrossCents,
            revenue.NetCents,
            doctor.Currency);
    }

    public async Task<IReadOnlyList<PeakHourDto>> PeakHoursAsync(DateRangeQuery query, CancellationToken ct)
    {
        var (doctor, range) = await ResolveAsync(query, ct);
        return await analytics.PeakHoursAsync(doctor.Id, range.FromUtc, range.ToUtc, doctor.TimeZone, ct);
    }

    public async Task<DoctorEarningsDto> EarningsAsync(DateRangeQuery query, CancellationToken ct)
    {
        var (doctor, range) = await ResolveAsync(query, ct);
        var revenue = await analytics.SumRevenueAsync(range.FromUtc, range.ToUtc, doctor.Id, ct);
        var payouts = await analytics.SumPayoutProgressAsync(range.FromUtc, range.ToUtc, doctor.Id, ct);
        return new DoctorEarningsDto(
            range.From,
            range.To,
            revenue.Payments,
            revenue.CapturedCents,
            revenue.RefundedCents,
            revenue.CommissionCents,
            revenue.ProviderFeeCents,
            revenue.NetCents,
            payouts.PendingCents,
            payouts.PaidCents,
            doctor.Currency);
    }

    private async Task<(Doctor Doctor, LocalDateRange Range)> ResolveAsync(DateRangeQuery query, CancellationToken ct)
    {
        var doctor = await doctors.MineAsync(actor, ct);
        return (doctor, LocalDateRange.Resolve(query.From, query.To, IanaTimeZone.Find(doctor.TimeZone), time.GetUtcNow()));
    }

    private static double Rate(int part, int whole) => whole == 0 ? 0 : Math.Round((double)part / whole, 4);
}
