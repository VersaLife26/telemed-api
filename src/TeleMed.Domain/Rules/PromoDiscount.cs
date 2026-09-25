using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Rules;

public static class PromoDiscount
{
    public static long Calculate(PromoCode promo, long amountCents)
    {
        if (amountCents <= 0 || amountCents < promo.MinAmountCents)
        {
            return 0;
        }

        var off = promo.DiscountType switch
        {
            PromoDiscountType.Percent => Math.Min(
                CommissionCalculator.RoundHalfUp(amountCents * (promo.PercentBps ?? 0), CommissionCalculator.BasisPoints),
                promo.MaxDiscountCents ?? long.MaxValue),
            PromoDiscountType.Fixed => promo.AmountOffCents ?? 0,
            _ => 0,
        };
        return Math.Min(off, amountCents);
    }

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
