using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Identity;

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider time) : ITokenIssuer
{
    public const string SecurityStampClaim = "stamp";

    private static readonly JsonWebTokenHandler Handler = new();

    public TimeSpan AccessTokenLifetime => options.Value.AccessTokenLifetime;
    public TimeSpan RefreshTokenLifetime => options.Value.RefreshTokenLifetime;

    public string IssueAccessToken(User user)
    {
        var o = options.Value;
        var now = time.GetUtcNow().UtcDateTime;
        return Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + o.AccessTokenLifetime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                ["role"] = UserRoleNames.Of(user.Role),
                [SecurityStampClaim] = user.SecurityStamp ?? "",
            },
            SigningCredentials = new SigningCredentials(o.CreateSecurityKey(), SecurityAlgorithms.HmacSha256),
        });
    }
}
