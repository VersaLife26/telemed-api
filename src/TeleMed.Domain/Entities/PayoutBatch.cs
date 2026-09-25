using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class PayoutBatch : Entity, IAuditable
{
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public PayoutBatchStatus Status { get; set; } = PayoutBatchStatus.Pending;
    public Guid? CreatedByAdminId { get; init; }
    public uint Version { get; init; }

    public void Refresh(IReadOnlyCollection<PayoutStatus> payouts)
    {
        var decided = payouts.Count(s => s != PayoutStatus.Pending);
        Status = decided == 0 ? PayoutBatchStatus.Pending
            : decided < payouts.Count ? PayoutBatchStatus.Processing
            : payouts.Contains(PayoutStatus.Failed) ? PayoutBatchStatus.Failed
            : PayoutBatchStatus.Paid;
    }
}
