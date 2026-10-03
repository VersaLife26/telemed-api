using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Scheduling;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Payouts;

// A run for local day D pays every captured payment not yet in a payout whose appointment ended as completed,
// once both the capture and that outcome are on or before D and older than PayoutHold, less the doctor's unapplied clawbacks.
// Anything the hold kept back (or a failed payout released) lands in a later day's batch.
// One batch per day and one payout per doctor per day make a re-run a no-op.
public sealed class PayoutService(
    ICurrentActor actor,
    IPayoutRepository payouts,
    IDoctorRepository doctors,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public static readonly TimeZoneInfo Zone = IanaTimeZone.Find(PlatformPolicy.TimeZoneId);

    public async Task<PagedResult<PayoutBatchDto>> ListBatchesAsync(PayoutBatchQuery query, CancellationToken ct)
    {
        var (items, total) = await payouts.ListBatchesAsync(query.Status, query.Skip, query.PageSize, ct);
        return new PagedResult<PayoutBatchDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<PayoutBatchDetailDto> GetBatchAsync(Guid id, CancellationToken ct)
    {
        var batch = await payouts.FindBatchDtoAsync(id, ct) ?? throw new NotFoundException("Payout batch not found.");
        return new PayoutBatchDetailDto(batch, await payouts.ListBatchPayoutsAsync(id, ct));
    }

    public async Task<PayoutRunDto> RunForAdminAsync(RunPayoutsRequest request, CancellationToken ct)
    {
        var admin = actor.RequireAdmin();
        var yesterday = IanaTimeZone.Today(time.GetUtcNow(), Zone).AddDays(-1);
        var period = request.Date ?? yesterday;
        if (period > yesterday)
        {
            throw new BadRequestException("period_not_closed", "Payouts can only be run for a day that has already ended.");
        }

        return await RunAsync(period, admin.Id, ct);
    }

    public async Task<PayoutRunDto> RunAsync(DateOnly period, Guid? adminId, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        if (await payouts.FindBatchByPeriodAsync(period, ct) is { } existing)
        {
            return new PayoutRunDto(period, false, await GetBatchAsync(existing.Id, ct));
        }

        var dayEnd = IanaTimeZone.StartOfDay(period.AddDays(1), Zone);
        var holdEnd = now - PlatformPolicy.PayoutHold;
        var payable = await payouts.ListPayableForUpdateAsync(holdEnd < dayEnd ? holdEnd : dayEnd, ct);
        var adjustments = (await payouts.ListUnappliedAdjustmentsForUpdateAsync(ct)).ToLookup(a => a.DoctorId);
        var clawedBack = await payouts.SumClawbacksAsync(payable.Select(p => p.Id).ToList(), ct);

        // A payment returning from a failed payout has its refunded share both in RefundedPayoutCents and in adjustments,
        // so the clawed-back part is added back here rather than deducted twice. A doctor whose adjustments outweigh the payable amount is skipped; both carry to a later run.
        // LKR and USD are separate payouts. An adjustment applies only to the payout in its own currency.
        var planned = payable
            .GroupBy(p => (p.DoctorId, p.Currency))
            .Select(g =>
            {
                var matched = adjustments[g.Key.DoctorId].Where(a => a.Currency == g.Key.Currency).ToList();
                return (
                    DoctorId: g.Key.DoctorId,
                    Currency: g.Key.Currency,
                    Payments: g.ToList(),
                    Adjustments: matched,
                    Amount: g.Sum(p => p.PayoutCents - p.RefundedPayoutCents + clawedBack.GetValueOrDefault(p.Id)) + matched.Sum(a => a.AmountCents));
            })
            .Where(x => x.Amount > 0)
            .ToList();
        if (planned.Count == 0)
        {
            return new PayoutRunDto(period, false, null);
        }

        var batch = new PayoutBatch
        {
            PeriodStart = planned.SelectMany(x => x.Payments).Min(p => SlotPlanner.LocalDate(p.SucceededAt!.Value, Zone)),
            PeriodEnd = period,
            CreatedByAdminId = adminId,
        };
        payouts.AddBatch(batch);
        foreach (var (doctorId, currency, payments, doctorAdjustments, amount) in planned)
        {
            var payout = new Payout
            {
                BatchId = batch.Id,
                DoctorId = doctorId,
                Period = period,
                AmountCents = amount,
                PaymentCount = payments.Count,
                Currency = currency,
            };
            payouts.Add(payout);
            foreach (var payment in payments)
            {
                payment.PayoutId = payout.Id;
            }

            foreach (var adjustment in doctorAdjustments)
            {
                adjustment.AppliedPayoutId = payout.Id;
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new PayoutRunDto(period, true, await GetBatchAsync(batch.Id, ct));
    }

    public async Task<PayoutDto> MarkPaidAsync(Guid id, MarkPayoutPaidRequest request, CancellationToken ct)
    {
        var admin = actor.RequireAdmin();
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var payout = await LoadPendingAsync(id, ct);
        payout.Status = PayoutStatus.Paid;
        payout.TransferReference = request.TransferReference.Trim();
        payout.PaidAt = time.GetUtcNow();
        payout.MarkedByAdminId = admin.Id;
        await RefreshBatchAsync(payout.BatchId, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (await payouts.FindDtoAsync(id, ct))!;
    }

    // The payments and the adjustments it deducted go back to the pool, so the next run settles them again in a new payout.
    public async Task<PayoutDto> MarkFailedAsync(Guid id, MarkPayoutFailedRequest request, CancellationToken ct)
    {
        var admin = actor.RequireAdmin();
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var payout = await LoadPendingAsync(id, ct);
        payout.Status = PayoutStatus.Failed;
        payout.FailureReason = request.Reason.Trim();
        payout.FailedAt = time.GetUtcNow();
        payout.MarkedByAdminId = admin.Id;
        foreach (var payment in await payouts.ListPaymentsForUpdateAsync(id, ct))
        {
            payment.PayoutId = null;
        }

        foreach (var adjustment in await payouts.ListAppliedAdjustmentsForUpdateAsync(id, ct))
        {
            adjustment.AppliedPayoutId = null;
        }

        await RefreshBatchAsync(payout.BatchId, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (await payouts.FindDtoAsync(id, ct))!;
    }

    public async Task<PagedResult<DoctorPayoutDto>> ListMineAsync(DoctorPayoutQuery query, CancellationToken ct)
    {
        var doctor = await doctors.MineAsync(actor, ct);
        var (items, total) = await payouts.ListForDoctorAsync(doctor.Id, query.Skip, query.PageSize, ct);
        var adjustments = (await payouts.ListAdjustmentsForPayoutsAsync(items.Select(p => p.Id).ToList(), ct)).ToLookup(a => a.AppliedPayoutId!.Value);
        return new PagedResult<DoctorPayoutDto>(
            items.Select(p => new DoctorPayoutDto(
                p.Id, p.Period, p.AmountCents, p.PaymentCount, p.Currency, p.Status, p.TransferReference, p.PaidAt, p.CreatedAt,
                adjustments[p.Id].Select(a => new PayoutAdjustmentDto(a.RefundId, a.PaymentId, a.AmountCents)).ToList())).ToList(),
            query.Page,
            query.PageSize,
            total);
    }

    private async Task<Payout> LoadPendingAsync(Guid id, CancellationToken ct)
    {
        var payout = await payouts.FindForUpdateAsync(id, ct) ?? throw new NotFoundException("Payout not found.");
        return payout.Status == PayoutStatus.Pending
            ? payout
            : throw new ConflictException("payout_not_pending", "Only a pending payout can be marked paid or failed.");
    }

    private async Task RefreshBatchAsync(Guid batchId, CancellationToken ct)
    {
        var batch = await payouts.FindBatchForUpdateAsync(batchId, ct) ?? throw new NotFoundException("Payout batch not found.");
        batch.Refresh((await payouts.ListBatchPayoutsForUpdateAsync(batchId, ct)).Select(p => p.Status).ToList());
    }
}
