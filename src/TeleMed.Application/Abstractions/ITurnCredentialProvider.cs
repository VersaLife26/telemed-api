using System.Text.Json.Serialization;

namespace TeleMed.Application.Abstractions;

// Shaped like an RTCIceServer so clients can pass it straight to RTCPeerConnection.
public sealed record IceServer(
    IReadOnlyList<string> Urls,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Username = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Credential = null);

public interface ITurnCredentialProvider
{
    Task<IReadOnlyList<IceServer>> GetIceServersAsync(CancellationToken ct);
}
