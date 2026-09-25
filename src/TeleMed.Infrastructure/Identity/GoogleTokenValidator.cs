using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Identity;

internal sealed class GoogleTokenValidator(IOptions<GoogleAuthOptions> options) : IGoogleTokenValidator
{
    public bool IsEnabled => options.Value.ClientIds.Length > 0;

    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = options.Value.ClientIds });
            return new GoogleIdentity(payload.Subject, payload.Email, payload.EmailVerified, payload.Name);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}
