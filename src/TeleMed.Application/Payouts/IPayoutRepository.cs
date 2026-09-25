using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Payouts;

public interface IPayoutRepository
{
    void AddBatch(PayoutBatch batch);
    void Add(Payout payout);
    Task<PayoutBatch?> FindBatchByPeriodAsync(DateOnly periodEnd, CancellationToken ct);
    Task<PayoutBatch?> FindBatchForUpdateAsync(Guid id, CancellationToken ct);
    Task<PayoutBatchDto?> FindBatchDtoAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<PayoutBatchDto> Items, long Total)> ListBatchesAsync(PayoutBatchStatus? status, int skip, int take, CancellationToken ct);
    Task<IReadOnlyList<PayoutDto>> ListBatchPayoutsAsync(Guid batchId, CancellationToken ct);
    Task<PayoutDto?> FindDtoAsync(Guid id, CancellationToken ct);
    Task<Payout?> FindForUpdateAsync(Guid id, CancellationToken ct);
    // Tracked; a payout already modified in this scope keeps its pending changes.
    Task<IReadOnlyList<Payout>> ListBatchPayoutsForUpdateAsync(Guid batchId, CancellationToken ct);
    Task<(IReadOnlyList<Payout> Items, long Total)> ListForDoctorAsync(Guid doctorId, int skip, int take, CancellationToken ct);

    // Captured, non-test payments not yet in a payout whose appointment is completed or no_show, whose capture and
    // appointment outcome both happened before the cutoff, and that have no refund still in flight; row-locked and tracked.
    Task<IReadOnlyList<Payment>> ListPayableForUpdateAsync(DateTimeOffset settledBefore, CancellationToken ct);
    Task<IReadOnlyList<Payment>> ListPaymentsForUpdateAsync(Guid payoutId, CancellationToken ct);

    void AddAdjustment(PayoutAdjustment adjustment);
    // Every doctor's adjustments not yet deducted from a payout; row-locked and tracked.
    Task<IReadOnlyList<PayoutAdjustment>> ListUnappliedAdjustmentsForUpdateAsync(CancellationToken ct);
    Task<IReadOnlyList<PayoutAdjustment>> ListAppliedAdjustmentsForUpdateAsync(Guid payoutId, CancellationToken ct);
    // Per payment, how much of its doctor share has been clawed back through adjustments (as a positive amount).
    Task<IReadOnlyList<PayoutAdjustment>> ListAdjustmentsForPayoutsAsync(IReadOnlyCollection<Guid> payoutIds, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, long>> SumClawbacksAsync(IReadOnlyCollection<Guid> paymentIds, CancellationToken ct);
}
