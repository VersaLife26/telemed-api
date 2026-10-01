using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Payouts;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class PayoutRepository(AppDbContext db) : IPayoutRepository
{
    public void AddBatch(PayoutBatch batch) => db.PayoutBatches.Add(batch);

    public void Add(Payout payout) => db.Payouts.Add(payout);

    public Task<PayoutBatch?> FindBatchByPeriodAsync(DateOnly periodEnd, CancellationToken ct) =>
        db.PayoutBatches.AsNoTracking().SingleOrDefaultAsync(b => b.PeriodEnd == periodEnd, ct);

    public async Task<PayoutBatch?> FindBatchForUpdateAsync(Guid id, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM payout_batches WHERE id = {id} FOR UPDATE", ct);
        return await db.PayoutBatches.SingleOrDefaultAsync(b => b.Id == id, ct);
    }

    public Task<PayoutBatchDto?> FindBatchDtoAsync(Guid id, CancellationToken ct) => BatchDtos(db.PayoutBatches.Where(b => b.Id == id)).SingleOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<PayoutBatchDto> Items, long Total)> ListBatchesAsync(PayoutBatchStatus? status, int skip, int take, CancellationToken ct)
    {
        var query = db.PayoutBatches.AsNoTracking();
        if (status is { } s)
        {
            query = query.Where(b => b.Status == s);
        }

        var total = await query.LongCountAsync(ct);
        var items = await BatchDtos(query.OrderByDescending(b => b.PeriodEnd).Skip(skip).Take(take)).ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<PayoutDto>> ListBatchPayoutsAsync(Guid batchId, CancellationToken ct) =>
        await WithAdjustmentsAsync(await PayoutDtos(db.Payouts.Where(p => p.BatchId == batchId)).ToListAsync(ct), ct);

    public async Task<PayoutDto?> FindDtoAsync(Guid id, CancellationToken ct) =>
        (await WithAdjustmentsAsync(await PayoutDtos(db.Payouts.Where(p => p.Id == id)).ToListAsync(ct), ct)).SingleOrDefault();

    public Task<Payout?> FindForUpdateAsync(Guid id, CancellationToken ct) => db.Payouts.SingleOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Payout>> ListBatchPayoutsForUpdateAsync(Guid batchId, CancellationToken ct) =>
        await db.Payouts.Where(p => p.BatchId == batchId).ToListAsync(ct);

    public async Task<(IReadOnlyList<Payout> Items, long Total)> ListForDoctorAsync(Guid doctorId, int skip, int take, CancellationToken ct)
    {
        var query = db.Payouts.AsNoTracking().Where(p => p.DoctorId == doctorId);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(p => p.Period).ThenBy(p => p.Id).Skip(skip).Take(take).ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<Payment>> ListPayableForUpdateAsync(DateTimeOffset settledBefore, CancellationToken ct) =>
        await db.Payments
            .FromSql($"""
                SELECT p.*, p.xmin FROM payments p
                JOIN appointments a ON a.id = p.appointment_id
                WHERE p.payout_id IS NULL
                  AND p.status IN ('succeeded', 'partially_refunded')
                  AND NOT a.is_test
                  AND a.status = 'completed'
                  AND GREATEST(p.succeeded_at, COALESCE(a.completed_at, a.no_show_at)) < {settledBefore}
                  AND NOT EXISTS (
                      SELECT 1 FROM refunds r
                      WHERE r.payment_id = p.id AND r.status IN ('requested', 'approved', 'processing', 'manual_required'))
                ORDER BY p.id
                FOR UPDATE OF p
                """)
            .ToListAsync(ct);

    public void AddAdjustment(PayoutAdjustment adjustment) => db.PayoutAdjustments.Add(adjustment);

    public async Task<IReadOnlyList<PayoutAdjustment>> ListUnappliedAdjustmentsForUpdateAsync(CancellationToken ct) =>
        await db.PayoutAdjustments
            .FromSql($"SELECT *, xmin FROM payout_adjustments WHERE applied_payout_id IS NULL ORDER BY id FOR UPDATE")
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PayoutAdjustment>> ListAppliedAdjustmentsForUpdateAsync(Guid payoutId, CancellationToken ct) =>
        await db.PayoutAdjustments
            .FromSql($"SELECT *, xmin FROM payout_adjustments WHERE applied_payout_id = {payoutId} ORDER BY id FOR UPDATE")
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PayoutAdjustment>> ListAdjustmentsForPayoutsAsync(IReadOnlyCollection<Guid> payoutIds, CancellationToken ct) =>
        await db.PayoutAdjustments.AsNoTracking()
            .Where(a => a.AppliedPayoutId != null && payoutIds.Contains(a.AppliedPayoutId.Value))
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, long>> SumClawbacksAsync(IReadOnlyCollection<Guid> paymentIds, CancellationToken ct) =>
        await db.PayoutAdjustments.AsNoTracking()
            .Where(a => paymentIds.Contains(a.PaymentId))
            .GroupBy(a => a.PaymentId)
            .Select(g => new { PaymentId = g.Key, Cents = -g.Sum(a => a.AmountCents) })
            .ToDictionaryAsync(x => x.PaymentId, x => x.Cents, ct);

    public async Task<IReadOnlyList<Payment>> ListPaymentsForUpdateAsync(Guid payoutId, CancellationToken ct) =>
        await db.Payments.FromSql($"SELECT *, xmin FROM payments WHERE payout_id = {payoutId} ORDER BY id FOR UPDATE").ToListAsync(ct);

    private IQueryable<PayoutBatchDto> BatchDtos(IQueryable<PayoutBatch> batches) =>
        batches.AsNoTracking().Select(b => new PayoutBatchDto(
            b.Id,
            b.PeriodStart,
            b.PeriodEnd,
            b.Status,
            db.Payouts.Count(p => p.BatchId == b.Id),
            db.Payouts.Where(p => p.BatchId == b.Id).Sum(p => (long?)p.AmountCents) ?? 0,
            b.CreatedByAdminId,
            b.CreatedAt));

    private IQueryable<PayoutDto> PayoutDtos(IQueryable<Payout> payouts) =>
        from p in payouts.AsNoTracking()
        join d in db.Doctors on p.DoctorId equals d.Id
        orderby d.DisplayName, p.Id
        select new PayoutDto(
            p.Id, p.BatchId, p.DoctorId, d.DisplayName, p.Period, p.AmountCents, p.PaymentCount, p.Currency, p.Status,
            p.TransferReference, p.FailureReason, p.PaidAt, p.FailedAt, p.MarkedByAdminId, p.CreatedAt, Array.Empty<PayoutAdjustmentDto>());

    private async Task<List<PayoutDto>> WithAdjustmentsAsync(List<PayoutDto> payouts, CancellationToken ct)
    {
        var adjustments = (await ListAdjustmentsForPayoutsAsync(payouts.Select(p => p.Id).ToList(), ct)).ToLookup(a => a.AppliedPayoutId!.Value);
        return payouts.Select(p => p with { Adjustments = adjustments[p.Id].Select(a => new PayoutAdjustmentDto(a.RefundId, a.PaymentId, a.AmountCents)).ToList() }).ToList();
    }
}
