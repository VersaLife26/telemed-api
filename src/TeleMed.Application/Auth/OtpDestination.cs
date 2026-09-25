using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Auth;

internal sealed record OtpDestination(string Address, MessageChannel Channel)
{
    public static OtpDestination? Parse(string? phone, string? email)
    {
        var hasPhone = !string.IsNullOrWhiteSpace(phone);
        var hasEmail = !string.IsNullOrWhiteSpace(email);
        if (hasPhone == hasEmail)
        {
            return null;
        }

        if (hasEmail)
        {
            return new OtpDestination(Emails.Normalize(email!), MessageChannel.Email);
        }

        return PhoneNumber.NormalizeSriLankanMobile(phone) is { } normalized
            ? new OtpDestination(normalized, MessageChannel.Sms)
            : null;
    }
}

internal static class Emails
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
