using TeleMed.Application.Common;

namespace TeleMed.Application.Analytics;

public enum RevenueGranularity
{
    Day,
    Week,
    Month,
}

public sealed record DashboardDto(
    DateOnly From,
    DateOnly To,
    int Bookings,
    int Completed,
    int Cancelled,
    int NoShows,
    long GrossRevenueCents,
    long CommissionCents,
    int ActiveDoctors,
    int NewPatients,
    string Currency);

public sealed record RevenuePointDto(DateOnly Period, int Payments, long CapturedCents, long RefundedCents, long NetCents, long CommissionCents);

public sealed record BookingsDayDto(DateOnly Date, int Total, int Confirmed, int Completed, int NoShow, int Cancelled);

public sealed record TopDoctorDto(Guid DoctorId, string DisplayName, int Completed, long NetCents, long CommissionCents);

public sealed record DoctorAnalyticsDto(
    DateOnly From,
    DateOnly To,
    int Consultations,
    int Completed,
    int NoShows,
    int Cancelled,
    double CompletionRate,
    double NoShowRate,
    long GrossCents,
    long NetCents,
    string Currency);

public sealed record PeakHourDto(int DayOfWeek, int Hour, int Count);

public sealed record DoctorEarningsDto(
    DateOnly From,
    DateOnly To,
    int Payments,
    long GrossCents,
    long RefundedCents,
    long CommissionCents,
    long ProviderFeeCents,
    long NetCents,
    long PendingPayoutCents,
    long PaidCents,
    string Currency);

public sealed record RevenueQuery : IDateRange
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public RevenueGranularity Granularity { get; init; } = RevenueGranularity.Day;
}

public sealed record TopDoctorsQuery : IDateRange
{
    public const int MaxLimit = 50;

    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int Limit { get; init; } = 10;
}

public sealed record BookingCounts(int Bookings, int Confirmed, int Completed, int NoShows, int Cancelled);

public sealed record RevenueTotals(int Payments, long CapturedCents, long RefundedCents, long CommissionCents, long ProviderFeeCents, long NetCents)
{
    public long GrossCents => CapturedCents - RefundedCents;
}

public sealed record PayoutProgress(long PendingCents, long PaidCents);
