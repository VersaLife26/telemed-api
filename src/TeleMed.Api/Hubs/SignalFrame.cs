using TeleMed.Application.Abstractions;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Hubs;

// Every hub message in both directions. Data is relayed untouched; From is stamped by the hub, never taken from the sender.
public sealed record SignalFrame(string? Type, string? From = null, object? Data = null);

public sealed record WelcomeData(string PeerId, UserRole Role, bool Polite, bool PeerPresent, IReadOnlyList<IceServer> IceServers);

public sealed record SignalError(string Code, string Message);

public static class SignalTypes
{
    public const string Ping = "ping";
    public const string Pong = "pong";
    public const string Welcome = "welcome";
    public const string PeerJoined = "peer-joined";
    public const string PeerLeft = "peer-left";
    public const string RoomClosed = "room-closed";
    public const string StateChanged = "state-changed";
    public const string Chat = "chat";
    public const string Error = "error";

    // Allowlist: a frame type not named here is never forwarded into the other browser.
    public static readonly IReadOnlySet<string> Relayable =
        new HashSet<string>(["offer", "answer", "ice", "bye", Chat, "pointer", "file_shared", "quality"], StringComparer.Ordinal);
}

public static class SignalErrorCodes
{
    public const string UnknownType = "UNKNOWN_TYPE";
    public const string Malformed = "MALFORMED";
    public const string NoPeer = "NO_PEER";
}
