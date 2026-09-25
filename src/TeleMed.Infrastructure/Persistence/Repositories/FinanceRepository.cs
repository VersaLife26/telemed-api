using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TeleMed.Application.Admin.Finance;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using static TeleMed.Infrastructure.Persistence.RawSql;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class FinanceRepository(AppDbContext db) : IFinanceRepository
{
    // Captured payments at their capture time and settled refunds (negated) at their settlement time.
    private const string LedgerSql = """
        SELECT 'payment' AS type, p.id, p.id AS payment_id, p.appointment_id, p.doctor_id, p.succeeded_at AS occurred_at,
               p.captured_cents AS amount_cents, p.commission_cents, p.provider_fee_cents, p.payout_cents,
               p.currency, p.provider, p.provider_payment_id AS reference
        FROM payments p
        JOIN appointments a ON a.id = p.appointment_id
        WHERE p.captured_cents IS NOT NULL AND NOT a.is_test
          AND p.succeeded_at >= @from AND p.succeeded_at < @to
          AND (@doctor IS NULL OR p.doctor_id = @doctor)
        UNION ALL
        SELECT 'refund', r.id, p.id, p.appointment_id, p.doctor_id, r.processed_at,
               -r.amount_cents, -r.commission_cents, -r.provider_fee_cents, -r.payout_cents,
               p.currency, p.provider, r.provider_refund_id
        FROM refunds r
        JOIN payments p ON p.id = r.payment_id
        JOIN appointments a ON a.id = p.appointment_id
        WHERE r.status = 'succeeded' AND NOT a.is_test
          AND r.processed_at >= @from AND r.processed_at < @to
          AND (@doctor IS NULL OR p.doctor_id = @doctor)
        """;

    public async Task<(IReadOnlyList<LedgerEntryDto> Items, long Total)> ListLedgerAsync(LedgerFilter filter, int skip, int take, CancellationToken ct)
    {
        var items = await db.QueryAsync(
            $"SELECT * FROM ({LedgerSql}) e ORDER BY occurred_at, id LIMIT @take OFFSET @skip",
            () => [.. LedgerParameters(filter), Param("take", take), Param("skip", skip)],
            ReadLedgerEntry,
            ct);
        var total = await db.QueryAsync($"SELECT count(*) FROM ({LedgerSql}) e", () => LedgerParameters(filter), r => r.GetInt64(0), ct);
        return (items, total[0]);
    }

    public async Task<LedgerTotalsDto> SumLedgerAsync(LedgerFilter filter, CancellationToken ct) =>
        (await db.QueryAsync(
            $"""
            SELECT count(*) FILTER (WHERE type = 'payment')::int,
                   count(*) FILTER (WHERE type = 'refund')::int,
                   COALESCE(sum(amount_cents) FILTER (WHERE type = 'payment'), 0)::bigint,
                   COALESCE(-sum(amount_cents) FILTER (WHERE type = 'refund'), 0)::bigint,
                   COALESCE(sum(amount_cents), 0)::bigint,
                   COALESCE(sum(commission_cents), 0)::bigint,
                   COALESCE(sum(provider_fee_cents), 0)::bigint,
                   COALESCE(sum(payout_cents), 0)::bigint
            FROM ({LedgerSql}) e
            """,
            () => LedgerParameters(filter),
            r => new LedgerTotalsDto(r.GetInt32(0), r.GetInt32(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4), r.GetInt64(5), r.GetInt64(6), r.GetInt64(7)),
            ct))[0];

    public IAsyncEnumerable<LedgerEntryDto> StreamLedgerAsync(LedgerFilter filter, CancellationToken ct) =>
        db.StreamAsync($"SELECT * FROM ({LedgerSql}) e ORDER BY occurred_at, id", () => LedgerParameters(filter), ReadLedgerEntry, ct);

    public async Task<(IReadOnlyList<AdminRefundDto> Items, long Total)> ListRefundsAsync(RefundStatus? status, int skip, int take, CancellationToken ct)
    {
        var query = db.Refunds.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(r => r.Status == s);
        }

        var total = await query.LongCountAsync(ct);
        var items = await RefundDtos(query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id).Skip(skip).Take(take)).ToListAsync(ct);
        return (items, total);
    }

    public Task<AdminRefundDto?> FindRefundAsync(Guid id, CancellationToken ct) => RefundDtos(db.Refunds.Where(r => r.Id == id)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AdminRefundDto>> ListRefundsForDisputeAsync(Guid disputeId, CancellationToken ct) =>
        await RefundDtos(db.Refunds.Where(r => r.DisputeId == disputeId)).ToListAsync(ct);

    public void AddPromoCode(PromoCode promoCode) => db.PromoCodes.Add(promoCode);

    public Task<PromoCode?> FindPromoCodeForUpdateAsync(Guid id, CancellationToken ct) => db.PromoCodes.SingleOrDefaultAsync(p => p.Id == id, ct);

    public async Task<(IReadOnlyList<PromoCodeDto> Items, long Total)> ListPromoCodesAsync(bool? isActive, int skip, int take, CancellationToken ct)
    {
        var query = db.PromoCodes.AsNoTracking();
        if (isActive is { } active)
        {
            query = query.Where(p => p.IsActive == active);
        }

        var total = await query.LongCountAsync(ct);
        var items = await PromoDtos(query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Skip(skip).Take(take)).ToListAsync(ct);
        return (items, total);
    }

    public Task<PromoCodeDto?> FindPromoCodeAsync(Guid id, CancellationToken ct) => PromoDtos(db.PromoCodes.Where(p => p.Id == id)).SingleOrDefaultAsync(ct);

    private static NpgsqlParameter[] LedgerParameters(LedgerFilter filter) =>
        [Param("from", filter.From), Param("to", filter.To), Uuid("doctor", filter.DoctorId)];

    private static LedgerEntryDto ReadLedgerEntry(DbDataReader r) => new(
        r.GetString(0) == "payment" ? LedgerEntryType.Payment : LedgerEntryType.Refund,
        r.GetGuid(1),
        r.GetGuid(2),
        r.GetGuid(3),
        r.GetGuid(4),
        r.Instant(5),
        r.GetInt64(6),
        r.GetInt64(7),
        r.GetInt64(8),
        r.GetInt64(9),
        r.GetString(10),
        r.NullableEnum<PaymentProvider>(11),
        r.NullableString(12));

    private IQueryable<AdminRefundDto> RefundDtos(IQueryable<Refund> refunds) =>
        from r in refunds.AsNoTracking()
        join p in db.Payments on r.PaymentId equals p.Id
        orderby r.CreatedAt descending, r.Id
        select new AdminRefundDto(
            r.Id, p.Id, p.AppointmentId, p.PatientId, p.DoctorId, r.AmountCents, r.CommissionCents, r.ProviderFeeCents, r.PayoutCents, r.Percent,
            p.Currency, r.Reason, r.Status, p.Provider, r.ProviderRefundId, r.FailureReason, r.Note, r.DisputeId, r.RequestedByAdminId,
            r.ReviewedByAdminId, r.ReviewedAt, r.RejectionReason, r.ProcessedAt, r.CreatedAt);

    private IQueryable<PromoCodeDto> PromoDtos(IQueryable<PromoCode> promos) =>
        promos.AsNoTracking().Select(p => new PromoCodeDto(
            p.Id, p.Code, p.Description, p.DiscountType, p.PercentBps, p.AmountOffCents, p.MaxDiscountCents, p.MinAmountCents, p.Currency,
            p.ValidFrom, p.ValidUntil, p.MaxRedemptions, p.MaxPerUser, p.IsActive,
            db.PromoRedemptions.Count(r => r.PromoCodeId == p.Id && r.Status == PromoRedemptionStatus.Consumed),
            p.CreatedAt));
}
