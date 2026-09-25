namespace TeleMed.Domain.Rules;

public static class PhoneNumber
{
    public static string? NormalizeSriLankanMobile(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith("94", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.Length == 10 && digits[0] == '0')
        {
            digits = digits[1..];
        }

        return digits.Length == 9 && digits[0] == '7' ? "+94" + digits : null;
    }
}
