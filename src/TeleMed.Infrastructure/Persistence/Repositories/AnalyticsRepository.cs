using Microsoft.EntityFrameworkCore;
using Npgsql;
using TeleMed.Application.Analytics;
using TeleMed.Domain.Enums;
using static TeleMed.Infrastructure.Persistence.RawSql;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class AnalyticsRepository(AppDbContext db) : IAnalyticsRepository
{
    private const string Bookings = """
        FROM appointments a
        WHERE NOT a.is_test AND a.confirmed_at IS NOT NULL
          AND a.start_at >= @from AND a.start_at < @to
          AND (@doctor IS NULL OR a.doctor_id = @doctor)
        """;

    private const string Captured = """
        FROM payments p
        JOIN appointments a ON a.id = p.appointment_id
        WHERE p.captured_cents IS NOT NULL AND NOT a.is_test
          AND p.succeeded_at >= @from AND p.succeeded_at < @to
          AND (@doctor IS NULL OR p.doctor_id = @doctor)
        """;

    public async Task<BookingCounts> CountBookingsAsync(DateTimeOffset from, DateTimeOffset to, Guid? doctorId, CancellationToken ct) =>
        (await db.QueryAsync(
            $"""
            SELECT count(*)::int,
                   count(*) FILTER (WHERE a.status = 'confirmed')::int,
                   count(*) FILTER (WHERE a.status = 'completed')::int,
                   count(*) FILTER (WHERE a.status = 'no_show')::int,
                   count(*) FILTER (WHERE a.status = 'cancelled')::int
            {Bookings}
            """,
            () => Range(from, to, doctorId),
            r => new BookingCounts(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4)),
            ct))[0];

    public async Task<RevenueTotals> SumRevenueAsync(DateTimeOffset from, DateTimeOffset to, Guid? doctorId, CancellationToken ct) =>
        (await db.QueryAsync(
            $"""
            SELECT count(*)::int,
                   COALESCE(sum(p.captured_cents), 0)::bigint,
                   COALESCE(sum(p.refunded_cents), 0)::bigint,
                   COALESCE(sum(p.commission_cents - p.refunded_commission_cents), 0)::bigint,
                   COALESCE(sum(p.provider_fee_cents - p.refunded_provider_fee_cents), 0)::bigint,
                   COALESCE(sum(p.payout_cents - p.refunded_payout_cents), 0)::bigint
            {Captured}
            """,
            () => Range(from, to, doctorId),
            r => new RevenueTotals(r.GetInt32(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4), r.GetInt64(5)),
            ct))[0];

    // A refund settled after its payment went into a payout lowers refunded_payout_cents and also leaves a clawback adjustment,
    // so each payment counts its clawed-back share back in and the adjustment is subtracted where it lands:
    // unapplied or in a pending payout lowers the pending figure, in a paid payout lowers the paid one.
    public async Task<PayoutProgress> SumPayoutProgressAsync(DateTimeOffset from, DateTimeOffset to, Guid doctorId, CancellationToken ct) =>
        (await db.QueryAsync(
            """
            WITH pay AS (
                SELECT p.id, po.status,
                       p.payout_cents - p.refunded_payout_cents
                           - COALESCE((SELECT sum(x.amount_cents) FROM payout_adjustments x WHERE x.payment_id = p.id), 0) AS net
                FROM payments p
                JOIN appointments a ON a.id = p.appointment_id
                LEFT JOIN payouts po ON po.id = p.payout_id
                WHERE p.captured_cents IS NOT NULL AND NOT a.is_test
                  AND p.succeeded_at >= @from AND p.succeeded_at < @to
                  AND p.doctor_id = @doctor
            ), adj AS (
                SELECT x.amount_cents, apo.status
                FROM payout_adjustments x
                JOIN pay ON pay.id = x.payment_id
                LEFT JOIN payouts apo ON apo.id = x.applied_payout_id
            )
            SELECT (SELECT COALESCE(sum(net) FILTER (WHERE status IS NULL OR status = 'pending'), 0) FROM pay)
                       + (SELECT COALESCE(sum(amount_cents) FILTER (WHERE status IS NULL OR status = 'pending'), 0) FROM adj),
                   (SELECT COALESCE(sum(net) FILTER (WHERE status = 'paid'), 0) FROM pay)
                       + (SELECT COALESCE(sum(amount_cents) FILTER (WHERE status = 'paid'), 0) FROM adj)
            """,
            () => Range(from, to, doctorId),
            r => new PayoutProgress(r.GetInt64(0), r.GetInt64(1)),
            ct))[0];

    public Task<int> CountActiveDoctorsAsync(CancellationToken ct) => db.Doctors.CountAsync(d => d.Status == DoctorStatus.Active, ct);

    public Task<int> CountNewPatientsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        db.Users.CountAsync(u => u.Role == UserRole.Patient && u.CreatedAt >= from && u.CreatedAt < to, ct);

    public async Task<IReadOnlyList<RevenuePointDto>> RevenueSeriesAsync(
        DateTimeOffset from, DateTimeOffset to, string timeZone, RevenueGranularity granularity, CancellationToken ct) =>
        await db.QueryAsync(
            $"""
            SELECT date_trunc(@unit, p.succeeded_at AT TIME ZONE @tz)::date,
                   count(*)::int,
                   sum(p.captured_cents)::bigint,
                   sum(p.refunded_cents)::bigint,
                   sum(p.captured_cents - p.refunded_cents)::bigint,
                   sum(p.commission_cents - p.refunded_commission_cents)::bigint
            {Captured}
            GROUP BY 1
            ORDER BY 1
            """,
            () => [.. Range(from, to, null), Param("tz", timeZone), Param("unit", granularity.ToString().ToLowerInvariant())],
            r => new RevenuePointDto(r.Date(0), r.GetInt32(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4), r.GetInt64(5)),
            ct);

    public async Task<IReadOnlyList<BookingsDayDto>> BookingsSeriesAsync(DateTimeOffset from, DateTimeOffset to, string timeZone, CancellationToken ct) =>
        await db.QueryAsync(
            $"""
            SELECT (a.start_at AT TIME ZONE @tz)::date,
                   count(*)::int,
                   count(*) FILTER (WHERE a.status = 'confirmed')::int,
                   count(*) FILTER (WHERE a.status = 'completed')::int,
                   count(*) FILTER (WHERE a.status = 'no_show')::int,
                   count(*) FILTER (WHERE a.status = 'cancelled')::int
            {Bookings}
            GROUP BY 1
            ORDER BY 1
            """,
            () => [.. Range(from, to, null), Param("tz", timeZone)],
            r => new BookingsDayDto(r.Date(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5)),
            ct);

    public async Task<IReadOnlyList<TopDoctorDto>> TopDoctorsAsync(DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct) =>
        await db.QueryAsync(
            $"""
            SELECT d.id, d.display_name,
                   (SELECT count(*) FROM appointments c
                    WHERE c.doctor_id = d.id AND NOT c.is_test AND c.status = 'completed'
                      AND c.start_at >= @from AND c.start_at < @to)::int,
                   r.net, r.commission
            FROM (
                SELECT p.doctor_id,
                       sum(p.captured_cents - p.refunded_cents)::bigint AS net,
                       sum(p.commission_cents - p.refunded_commission_cents)::bigint AS commission
                {Captured}
                GROUP BY p.doctor_id
            ) r
            JOIN doctors d ON d.id = r.doctor_id
            ORDER BY r.net DESC, d.id
            LIMIT @limit
            """,
            () => [.. Range(from, to, null), Param("limit", limit)],
            r => new TopDoctorDto(r.GetGuid(0), r.GetString(1), r.GetInt32(2), r.GetInt64(3), r.GetInt64(4)),
            ct);

    public async Task<IReadOnlyList<PeakHourDto>> PeakHoursAsync(Guid doctorId, DateTimeOffset from, DateTimeOffset to, string timeZone, CancellationToken ct) =>
        await db.QueryAsync(
            $"""
            SELECT extract(dow FROM a.start_at AT TIME ZONE @tz)::int,
                   extract(hour FROM a.start_at AT TIME ZONE @tz)::int,
                   count(*)::int
            {Bookings}
              AND a.status <> 'cancelled'
            GROUP BY 1, 2
            ORDER BY 1, 2
            """,
            () => [.. Range(from, to, doctorId), Param("tz", timeZone)],
            r => new PeakHourDto(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2)),
            ct);

    private static NpgsqlParameter[] Range(DateTimeOffset from, DateTimeOffset to, Guid? doctorId) =>
        [Param("from", from), Param("to", to), Uuid("doctor", doctorId)];
}
