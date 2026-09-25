using TeleMed.Application.Abstractions;

namespace TeleMed.Api.IntegrationTests.Infrastructure;

// Accepts tokens shaped "sub|email|verified|name" so tests can drive every Google branch without Google.
internal sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    public bool IsEnabled => true;

    public Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct)
    {
        var parts = idToken.Split('|');
        return Task.FromResult(parts.Length == 4
            ? new GoogleIdentity(parts[0], parts[1], bool.Parse(parts[2]), parts[3])
            : null);
    }

    public static string Token(string subject, string email, bool verified = true, string name = "Google User") =>
        $"{subject}|{email}|{verified}|{name}";
}
