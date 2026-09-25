using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Video;

// Turn:Provider=None (STUN only) and Turn:Provider=Static (a fixed TURN server and credential).
internal sealed class StaticTurnProvider(IOptions<TurnOptions> options) : ITurnCredentialProvider
{
    public Task<IReadOnlyList<IceServer>> GetIceServersAsync(CancellationToken ct)
    {
        var turn = options.Value;
        var servers = new List<IceServer>();
        if (turn.Stun.Count > 0)
        {
            servers.Add(new IceServer(turn.Stun));
        }

        if (turn.Provider == TurnProvider.Static)
        {
            servers.Add(new IceServer(turn.StaticUrls, NullIfEmpty(turn.Username), NullIfEmpty(turn.Credential)));
        }

        return Task.FromResult<IReadOnlyList<IceServer>>(servers);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
