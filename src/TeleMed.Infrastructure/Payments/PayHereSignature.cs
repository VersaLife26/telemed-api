using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TeleMed.Infrastructure.Payments;

// PayHere's hashes are MD5 over an undelimited concatenation, so "LKR" + "-2" (a genuine failure) and "LKR-" + "2"
// (a forged success) share a preimage and a valid signature. Every hashed field is held to its own grammar before
// hashing, which fixes each boundary: the order id is a canonical 36-char UUID, the amount uses only [0-9.],
// the currency is exactly three [A-Z], so the status code starts at a known offset.
internal static partial class PayHereSignature
{
    public static string FormatAmount(long cents)
    {
        var sign = cents < 0 ? "-" : "";
        var abs = Math.Abs(cents);
        return $"{sign}{abs / 100}.{abs % 100:D2}";
    }

    // Only called on strings that passed the amount grammar.
    public static long ParseAmount(string amount)
    {
        var parts = amount.Split('.');
        var cents = long.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) * 100;
        return parts.Length == 2 ? cents + long.Parse(parts[1].PadRight(2, '0'), System.Globalization.CultureInfo.InvariantCulture) : cents;
    }

    public static string Md5Upper(string value) => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value)));

    public static string CheckoutHash(string merchantId, string orderId, string amount, string currency, string secretHash) =>
        Md5Upper(merchantId + orderId + amount + currency + secretHash);

    public static string NotifyHash(string merchantId, string orderId, string amount, string currency, string statusCode, string secretHash) =>
        Md5Upper(merchantId + orderId + amount + currency + statusCode + secretHash);

    public static bool IsUnambiguous(string orderId, string amount, string currency, string statusCode) =>
        IsCanonicalUuid(orderId) && AmountGrammar().IsMatch(amount) && CurrencyGrammar().IsMatch(currency) && StatusGrammar().IsMatch(statusCode);

    public static bool IsCanonicalUuid(string value) =>
        value.Length == 36 && Guid.TryParseExact(value, "D", out var id) && id.ToString("D") == value;

    public static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    [GeneratedRegex(@"^[0-9]{1,15}(\.[0-9]{1,2})?\z")]
    private static partial Regex AmountGrammar();

    [GeneratedRegex(@"^[A-Z]{3}\z")]
    private static partial Regex CurrencyGrammar();

    [GeneratedRegex(@"^-?[0-9]{1,2}\z")]
    private static partial Regex StatusGrammar();
}
