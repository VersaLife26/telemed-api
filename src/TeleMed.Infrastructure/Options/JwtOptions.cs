using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace TeleMed.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string Section = "Auth:Jwt";
    public const int MinKeyBytes = 32;

    public string Issuer { get; set; } = "telemed-api";
    public string Audience { get; set; } = "telemed-api";
    public string SigningKey { get; set; } = "";
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan SessionCacheDuration { get; set; } = TimeSpan.FromSeconds(30);

    public SymmetricSecurityKey CreateSecurityKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
