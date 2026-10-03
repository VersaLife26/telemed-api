using System.Text.RegularExpressions;

namespace TeleMed.Domain.Rules;

// Old NIC: 9 digits and a V or X letter. New NIC: 12 digits. Spaces are ignored; the letter is stored upper case.
public static partial class NationalId
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = (value ?? "").Trim().Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        return Old().IsMatch(normalized) || New().IsMatch(normalized);
    }

    [GeneratedRegex(@"^[0-9]{9}[VX]\z")]
    private static partial Regex Old();

    [GeneratedRegex(@"^[0-9]{12}\z")]
    private static partial Regex New();
}
