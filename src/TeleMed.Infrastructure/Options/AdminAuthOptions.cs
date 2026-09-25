using System.Net;
using System.Text;

namespace TeleMed.Infrastructure.Options;

public sealed class AdminAuthOptions
{
    public const string Section = "AdminAuth";

    public CloudflareAccessOptions CloudflareAccess { get; set; } = new();
    public LocalAdminJwtOptions LocalJwt { get; set; } = new();
    public string[] IpAllowlist { get; set; } = [];
    public bool AllowAnyIp { get; set; }
    public string? BootstrapSuperAdminEmail { get; set; }
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(30);

    public bool TryParseAllowlist(out IReadOnlyList<IPNetwork> networks)
    {
        var parsed = new List<IPNetwork>();
        networks = parsed;
        foreach (var entry in IpAllowlist.Select(e => e.Trim()))
        {
            if (IPNetwork.TryParse(entry, out var network))
            {
                parsed.Add(network);
            }
            else if (IPAddress.TryParse(entry, out var address))
            {
                parsed.Add(new IPNetwork(address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128));
            }
            else
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class CloudflareAccessOptions
{
    public bool Enabled { get; set; } = true;

    // The team domain, e.g. "myteam.cloudflareaccess.com".
    public string TeamDomain { get; set; } = "";
    public string Audience { get; set; } = "";

    public string Host => TeamDomain.Trim().TrimEnd('/').Replace("https://", "", StringComparison.OrdinalIgnoreCase);
    public string Issuer => $"https://{Host}";
    public string CertsUrl => $"{Issuer}/cdn-cgi/access/certs";
}

public sealed class LocalAdminJwtOptions
{
    public const string Issuer = "telemed-admin-local";
    public const string Audience = "telemed-admin";

    public bool Enabled { get; set; }
    public string SigningKey { get; set; } = "";

    public byte[] KeyBytes => Encoding.UTF8.GetBytes(SigningKey);
}
