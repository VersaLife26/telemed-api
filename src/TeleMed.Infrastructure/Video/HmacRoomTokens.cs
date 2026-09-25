using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Video;

// "{consultationId}.{userId}.{role}.{expiresUnix}.{hmac}". Every field has a fixed alphabet without dots, so fields cannot shift.
internal sealed class HmacRoomTokens(IOptions<VideoOptions> options, TimeProvider time) : IRoomTokens
{
    public string Issue(RoomGrant grant)
    {
        var expires = (time.GetUtcNow() + options.Value.RoomTokenLifetime).ToUnixTimeSeconds();
        var body = $"{grant.ConsultationId:N}.{grant.UserId:N}.{UserRoleNames.Of(grant.Role)}.{expires.ToString(CultureInfo.InvariantCulture)}";
        return $"{body}.{Sign(body)}";
    }

    // The signature is checked before anything in the body is trusted, the expiry included.
    public RoomGrant? Validate(string? token)
    {
        var cut = token?.LastIndexOf('.') ?? -1;
        if (cut <= 0)
        {
            return null;
        }

        var body = token![..cut];
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(token[(cut + 1)..]), Encoding.ASCII.GetBytes(Sign(body))))
        {
            return null;
        }

        var parts = body.Split('.');
        return parts.Length == 4
            && Guid.TryParseExact(parts[0], "N", out var consultationId)
            && Guid.TryParseExact(parts[1], "N", out var userId)
            && UserRoleNames.Parse(parts[2]) is { } role
            && long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
            && time.GetUtcNow().ToUnixTimeSeconds() < expires
                ? new RoomGrant(consultationId, userId, role)
                : null;
    }

    private string Sign(string body) => Base64Url.EncodeToString(HMACSHA256.HashData(options.Value.KeyBytes, Encoding.UTF8.GetBytes(body)));
}
