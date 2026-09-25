namespace TeleMed.Infrastructure.Options;

public enum TurnProvider
{
    None,
    Static,
    Cloudflare,
}

public sealed class TurnOptions
{
    public const string Section = "Turn";

    public TurnProvider Provider { get; set; } = TurnProvider.None;
    private static readonly string[] DefaultStunUrls = ["stun:stun.cloudflare.com:3478"];

    // Served with every provider; also the only entry when TURN is off or Cloudflare cannot be reached.
    // Nullable because the configuration binder appends to, rather than replaces, a pre-filled array.
    public string[]? StunUrls { get; set; }
    public string[] StaticUrls { get; set; } = [];
    public string? Username { get; set; }
    public string? Credential { get; set; }
    public CloudflareTurnOptions Cloudflare { get; set; } = new();

    public IReadOnlyList<string> Stun => StunUrls ?? DefaultStunUrls;

    public bool IsValid() => Provider switch
    {
        TurnProvider.Static => StaticUrls.Length > 0
            && StaticUrls.All(u => u.StartsWith("turn:", StringComparison.Ordinal) || u.StartsWith("turns:", StringComparison.Ordinal))
            && string.IsNullOrEmpty(Username) == string.IsNullOrEmpty(Credential),
        TurnProvider.Cloudflare => Cloudflare.KeyId.Length > 0 && Cloudflare.ApiToken.Length > 0 && Cloudflare.TtlSeconds > 0
            && Cloudflare.Timeout > TimeSpan.Zero,
        _ => true,
    };
}

public sealed class CloudflareTurnOptions
{
    public string KeyId { get; set; } = "";
    public string ApiToken { get; set; } = "";
    // Must outlast the longest consultation: a credential that expires mid-call takes the relay with it.
    public int TtlSeconds { get; set; } = 12 * 60 * 60;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
}
