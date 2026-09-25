using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class CommissionCalculatorTests
{
    [Theory]
    [InlineData(250_000, 50_000, 7_500, 192_500)]
    [InlineData(100, 20, 3, 77)]
    [InlineData(333, 67, 10, 256)]
    [InlineData(25, 5, 1, 19)]
    [InlineData(1, 0, 0, 1)]
    public void Split_rounds_half_up_and_payout_takes_the_rest(long amount, long commission, long fee, long payout)
    {
        CommissionCalculator.Split(amount).ShouldBe(new PaymentSplit(amount, commission, fee, payout));
    }

    [Fact]
    public void Split_always_balances()
    {
        for (long amount = 1; amount < 5_000; amount++)
        {
            var split = CommissionCalculator.Split(amount);
            (split.CommissionCents + split.ProviderFeeCents + split.PayoutCents).ShouldBe(amount);
            split.PayoutCents.ShouldBeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public void Full_refund_returns_every_bucket()
    {
        var paid = CommissionCalculator.Split(250_000);
        CommissionCalculator.Prorate(paid, 250_000).ShouldBe(paid);
    }

    [Fact]
    public void Partial_refund_is_proportional_and_balances()
    {
        var paid = CommissionCalculator.Split(250_000);
        CommissionCalculator.Prorate(paid, 125_000).ShouldBe(new PaymentSplit(125_000, 25_000, 3_750, 96_250));

        var odd = CommissionCalculator.Split(333);
        var back = CommissionCalculator.Prorate(odd, 167);
        (back.CommissionCents + back.ProviderFeeCents + back.PayoutCents).ShouldBe(167);
        back.CommissionCents.ShouldBe(34);
        back.ProviderFeeCents.ShouldBe(5);
    }

    [Theory]
    [InlineData(250_000, 50, 125_000)]
    [InlineData(333, 50, 167)]
    [InlineData(1, 50, 1)]
    [InlineData(999, 0, 0)]
    [InlineData(999, 100, 999)]
    public void Percent_of_rounds_half_up(long amount, int percent, long expected)
    {
        CommissionCalculator.PercentOf(amount, percent).ShouldBe(expected);
    }

    [Theory]
    [InlineData(5, 10, 1)]
    [InlineData(4, 10, 0)]
    [InlineData(15, 10, 2)]
    [InlineData(20, 10, 2)]
    public void Round_half_up(long numerator, long denominator, long expected)
    {
        CommissionCalculator.RoundHalfUp(numerator, denominator).ShouldBe(expected);
    }
}
