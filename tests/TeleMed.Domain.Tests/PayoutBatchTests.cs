using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Tests;

public class PayoutBatchTests
{
    [Theory]
    [InlineData(new[] { PayoutStatus.Pending, PayoutStatus.Pending }, PayoutBatchStatus.Pending)]
    [InlineData(new[] { PayoutStatus.Paid, PayoutStatus.Pending }, PayoutBatchStatus.Processing)]
    [InlineData(new[] { PayoutStatus.Paid, PayoutStatus.Paid }, PayoutBatchStatus.Paid)]
    [InlineData(new[] { PayoutStatus.Paid, PayoutStatus.Cancelled }, PayoutBatchStatus.Paid)]
    [InlineData(new[] { PayoutStatus.Paid, PayoutStatus.Failed }, PayoutBatchStatus.Failed)]
    public void Status_follows_its_payouts(PayoutStatus[] payouts, PayoutBatchStatus expected)
    {
        var batch = new PayoutBatch();
        batch.Refresh(payouts);
        batch.Status.ShouldBe(expected);
    }
}
