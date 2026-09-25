using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Users;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Auth;

public sealed class SessionIssuer(
    ITokenIssuer tokens,
    IRefreshTokenRepository refreshTokens,
    IUserAccounts accounts,
    IFileStorage storage,
    TimeProvider time)
{
    public IssuedSession Issue(User user, Guid? familyId = null)
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            FamilyId = familyId ?? Guid.CreateVersion7(),
            TokenHash = Hash(raw),
            ExpiresAt = time.GetUtcNow() + tokens.RefreshTokenLifetime,
        };
        refreshTokens.Add(refreshToken);

        var response = new AuthResponse(
            tokens.IssueAccessToken(user),
            raw,
            (int)tokens.AccessTokenLifetime.TotalSeconds,
            user.ToMeDto(storage));
        return new IssuedSession(response, refreshToken);
    }

    public async Task RevokeAllAsync(User user, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        foreach (var token in await refreshTokens.ListActiveByUserAsync(user.Id, ct))
        {
            token.RevokedAt = now;
        }

        await accounts.UpdateSecurityStampAsync(user);
    }

    public static string Hash(string rawToken) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

public sealed record IssuedSession(AuthResponse Response, RefreshToken RefreshToken);
