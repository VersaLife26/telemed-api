namespace TeleMed.Domain.Rules;

public readonly record struct PaymentSplit(long AmountCents, long CommissionCents, long ProviderFeeCents, long PayoutCents);

public static class CommissionCalculator
{
    public const int BasisPoints = 10_000;

    public static PaymentSplit Split(long amountCents, int commissionBps = PlatformPolicy.CommissionBps)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountCents);
        ArgumentOutOfRangeException.ThrowIfNegative(commissionBps);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(commissionBps, PlatformPolicy.MaxCommissionBps);
        var commission = RoundHalfUp(amountCents * commissionBps, BasisPoints);
        var fee = RoundHalfUp(amountCents * PlatformPolicy.ProviderFeeBps, BasisPoints) + PlatformPolicy.ProviderFeeFixedCents;
        return new PaymentSplit(amountCents, commission, fee, amountCents - commission - fee);
    }

    // Each bucket gives back its share of the refund; payout takes the rounding remainder so the three always sum to the refund.
    public static PaymentSplit Prorate(PaymentSplit paid, long refundCents)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(refundCents);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(refundCents, paid.AmountCents);
        if (refundCents == paid.AmountCents)
        {
            return paid;
        }

        var commission = RoundHalfUp(paid.CommissionCents * refundCents, paid.AmountCents);
        var fee = RoundHalfUp(paid.ProviderFeeCents * refundCents, paid.AmountCents);
        return new PaymentSplit(refundCents, commission, fee, refundCents - commission - fee);
    }

    public static long PercentOf(long amountCents, int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountCents);
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        return RoundHalfUp(amountCents * percent, 100);
    }

    public static long RoundHalfUp(long numerator, long denominator)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(numerator);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(denominator);
        var quotient = numerator / denominator;
        return 2 * (numerator % denominator) >= denominator ? quotient + 1 : quotient;
    }
}
