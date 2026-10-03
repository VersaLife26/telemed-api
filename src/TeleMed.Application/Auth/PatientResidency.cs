using TeleMed.Application.Abstractions;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Auth;

// The IP country only decides whether the citizenship question is asked. A connection outside Sri Lanka,
// including a Sri Lankan citizen abroad, is an international patient and is not asked for a National ID.
internal static class PatientResidency
{
    public const string SriLanka = "LK";

    public static bool AsksCitizenship(string? countryCode) =>
        string.Equals(countryCode, SriLanka, StringComparison.OrdinalIgnoreCase);

    public static void Apply(User user, string? countryCode, bool? isSriLankanCitizen, string? nationalId, IBankDataCipher cipher)
    {
        user.RegistrationCountry = countryCode;
        user.IsSriLankanCitizen = false;
        user.NationalIdEncrypted = null;
        if (!AsksCitizenship(countryCode))
        {
            return;
        }

        if (isSriLankanCitizen is null)
        {
            throw new BadRequestException("citizenship_required", "Say whether you are a Sri Lankan citizen.");
        }

        if (isSriLankanCitizen == false)
        {
            if (!string.IsNullOrWhiteSpace(nationalId))
            {
                throw new BadRequestException("national_id_not_applicable", "A National ID is only collected for Sri Lankan citizens.");
            }

            return;
        }

        if (!NationalId.TryNormalize(nationalId, out var normalized))
        {
            throw new BadRequestException("invalid_national_id", "Enter a valid Sri Lankan National ID.");
        }

        user.IsSriLankanCitizen = true;
        user.NationalIdEncrypted = cipher.Encrypt(normalized);
    }
}
