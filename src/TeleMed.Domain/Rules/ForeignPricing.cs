namespace TeleMed.Domain.Rules;

// An international patient pays the doctor's LKR fee times an admin multiplier, converted to USD
// with the platform's LKR-per-USD rate. 2,500 LKR × 4 at 300 LKR/USD is 33.33 USD, not 10,000 USD.
public static class ForeignPricing
{
    public const string Currency = "USD";
    public const decimal MinMultiplier = 1m;
    public const decimal MaxMultiplier = 100m;
    public const decimal MinLkrPerUsd = 1m;
    public const decimal MaxLkrPerUsd = 100_000m;

    public static long UsdCents(long feeCents, decimal multiplier, decimal lkrPerUsd)
    {
        if (TryUsdCents(feeCents, multiplier, lkrPerUsd) is not { } cents)
        {
            throw new ArgumentOutOfRangeException(nameof(feeCents), "The international fee cannot be priced from these inputs.");
        }

        return cents;
    }

    public static long? TryUsdCents(long feeCents, decimal multiplier, decimal lkrPerUsd)
    {
        if (feeCents <= 0 || multiplier < MinMultiplier || multiplier > MaxMultiplier
            || lkrPerUsd < MinLkrPerUsd || lkrPerUsd > MaxLkrPerUsd)
        {
            return null;
        }

        var usd = decimal.Round(feeCents * multiplier / lkrPerUsd, 0, MidpointRounding.AwayFromZero);
        if (usd < 1 || usd > long.MaxValue)
        {
            return null;
        }

        return (long)usd;
    }
}
