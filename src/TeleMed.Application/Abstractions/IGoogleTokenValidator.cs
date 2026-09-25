namespace TeleMed.Application.Abstractions;

public interface IGoogleTokenValidator
{
    bool IsEnabled { get; }
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken ct);
}

public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string? Name);
