namespace TeleMed.Domain.Rules;

// PayHere releases a hold after 7 days, so a hold is only offered inside the 6-day window.
// Each currency has its own switch. Off means the card is charged immediately.
public static class CardHold
{
    public static bool Applies(string currency, bool withinSixDays, bool holdLkrWithinSixDays, bool holdUsdWithinSixDays)
    {
        if (!withinSixDays)
        {
            return false;
        }

        return string.Equals(currency, ForeignPricing.Currency, StringComparison.OrdinalIgnoreCase)
            ? holdUsdWithinSixDays
            : holdLkrWithinSixDays;
    }
}
