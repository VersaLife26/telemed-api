namespace TeleMed.Application.Analytics;

// Live SQL over appointments and payments. Test appointments never count. A booking is a non-test appointment starting in the
// range that was confirmed at some point, so unpaid holds that expired are not bookings; revenue is captured money by capture time.
public interface IAnalyticsRepository
{
    Task<BookingCounts> CountBookingsAsync(DateTimeOffset from, DateTimeOffset to, Guid? doctorId, CancellationToken ct);
    Task<RevenueTotals> SumRevenueAsync(DateTimeOffset from, DateTimeOffset to, Guid? doctorId, CancellationToken ct);
    Task<PayoutProgress> SumPayoutProgressAsync(DateTimeOffset from, DateTimeOffset to, Guid doctorId, CancellationToken ct);
    Task<int> CountActiveDoctorsAsync(CancellationToken ct);
    Task<int> CountNewPatientsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<IReadOnlyList<RevenuePointDto>> RevenueSeriesAsync(DateTimeOffset from, DateTimeOffset to, string timeZone, RevenueGranularity granularity, CancellationToken ct);
    Task<IReadOnlyList<BookingsDayDto>> BookingsSeriesAsync(DateTimeOffset from, DateTimeOffset to, string timeZone, CancellationToken ct);
    Task<IReadOnlyList<TopDoctorDto>> TopDoctorsAsync(DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct);
    // Held and upcoming consultations (cancelled ones excluded) by local day of week (0 = Sunday) and hour.
    Task<IReadOnlyList<PeakHourDto>> PeakHoursAsync(Guid doctorId, DateTimeOffset from, DateTimeOffset to, string timeZone, CancellationToken ct);
}
