using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class ForeignPricingTests
{
    [Theory]
    [InlineData("123456789V", "123456789V")]
    [InlineData(" 123456789x ", "123456789X")]
    [InlineData("200012345678", "200012345678")]
    public void National_ids_normalize(string raw, string expected)
    {
        NationalId.TryNormalize(raw, out var normalized).ShouldBeTrue();
        normalized.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789")]
    [InlineData("123456789VX")]
    [InlineData("1234567890")]
    public void Malformed_national_ids_are_rejected(string raw)
    {
        NationalId.TryNormalize(raw, out _).ShouldBeFalse();
    }

    [Fact]
    public void A_multiplier_converts_rupees_into_dollars()
    {
        // 2,500.00 LKR × 4 at 300 LKR per USD = 33.33 USD.
        ForeignPricing.UsdCents(250_000, 4m, 300m).ShouldBe(3_333);
        ForeignPricing.UsdCents(250_000, 5m, 300m).ShouldBe(4_167);
    }

    [Fact]
    public void An_unpriceable_combination_is_refused()
    {
        ForeignPricing.TryUsdCents(250_000, 0.5m, 300m).ShouldBeNull();
        ForeignPricing.TryUsdCents(1, 1m, 100_000m).ShouldBeNull();
    }
}

public class CardHoldTests
{
    [Theory]
    [InlineData("LKR", true, true, false, true)]
    [InlineData("LKR", true, false, true, false)]
    [InlineData("USD", true, true, false, false)]
    [InlineData("USD", true, false, true, true)]
    [InlineData("usd", true, false, true, true)]
    [InlineData("LKR", false, true, true, false)]
    [InlineData("USD", false, true, true, false)]
    public void Each_currency_has_its_own_switch_and_only_inside_six_days(
        string currency, bool withinSixDays, bool holdLkr, bool holdUsd, bool expected)
    {
        CardHold.Applies(currency, withinSixDays, holdLkr, holdUsd).ShouldBe(expected);
    }
}
