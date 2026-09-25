using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class PromoDiscountTests
{
    private static PromoCode Percent(int bps, long? cap = null, long min = 0) =>
        new() { Code = "P", DiscountType = PromoDiscountType.Percent, PercentBps = bps, MaxDiscountCents = cap, MinAmountCents = min };

    private static PromoCode Fixed(long off, long min = 0) =>
        new() { Code = "F", DiscountType = PromoDiscountType.Fixed, AmountOffCents = off, MinAmountCents = min };

    [Theory]
    [InlineData(1_000, 250_000, 25_000)]
    [InlineData(1_250, 333, 42)]
    [InlineData(10_000, 5_000, 5_000)]
    public void Percent_rounds_half_up(int bps, long amount, long expected)
    {
        PromoDiscount.Calculate(Percent(bps), amount).ShouldBe(expected);
    }

    [Fact]
    public void Percent_is_capped()
    {
        PromoDiscount.Calculate(Percent(5_000, cap: 10_000), 250_000).ShouldBe(10_000);
        PromoDiscount.Calculate(Percent(5_000, cap: 10_000), 10_000).ShouldBe(5_000);
    }

    [Fact]
    public void Fixed_is_capped_at_the_amount()
    {
        PromoDiscount.Calculate(Fixed(50_000), 250_000).ShouldBe(50_000);
        PromoDiscount.Calculate(Fixed(50_000), 30_000).ShouldBe(30_000);
    }

    [Fact]
    public void Below_the_minimum_amount_there_is_no_discount()
    {
        PromoDiscount.Calculate(Fixed(1_000, min: 100_000), 99_999).ShouldBe(0);
        PromoDiscount.Calculate(Fixed(1_000, min: 100_000), 100_000).ShouldBe(1_000);
        PromoDiscount.Calculate(Percent(1_000, min: 100_000), 50_000).ShouldBe(0);
    }

    [Fact]
    public void Codes_are_normalised()
    {
        PromoDiscount.Normalize("  welcome10 ").ShouldBe("WELCOME10");
    }
}
