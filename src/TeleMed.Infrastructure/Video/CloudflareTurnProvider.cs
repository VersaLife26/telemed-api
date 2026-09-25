using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Video;

// Cloudflare Realtime TURN mints credentials over HTTP. Every join (and every signalling reconnect) asks for ICE servers,
// so credentials are cached for half their lifetime; when Cloudflare is unreachable a stale credential beats STUN only,
// which cannot connect from behind carrier-grade NAT.
internal sealed class CloudflareTurnProvider(
    IHttpClientFactory httpClients,
    IOptions<TurnOptions> options,
    TimeProvider time,
    ILogger<CloudflareTurnProvider> logger) : ITurnCredentialProvider
{
    public const string HttpClientName = "CloudflareTurn";
    public const string BaseUrl = "https://rtc.live.cloudflare.com";

    private readonly SemaphoreSlim _refresh = new(1, 1);
    private IReadOnlyList<IceServer>? _cached;
    private DateTimeOffset _freshUntil;

    public async Task<IReadOnlyList<IceServer>> GetIceServersAsync(CancellationToken ct)
    {
        if (_cached is { } fresh && time.GetUtcNow() < _freshUntil)
        {
            return fresh;
        }

        await _refresh.WaitAsync(ct);
        try
        {
            if (_cached is { } refreshed && time.GetUtcNow() < _freshUntil)
            {
                return refreshed;
            }

            var cloudflare = options.Value.Cloudflare;
            try
            {
                var servers = await FetchAsync(cloudflare, ct);
                _cached = servers;
                _freshUntil = time.GetUtcNow() + TimeSpan.FromSeconds(cloudflare.TtlSeconds) / 2;
                return servers;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                if (_cached is { } stale)
                {
                    logger.LogWarning(ex, "Cloudflare TURN unavailable; reusing the previous credential");
                    return stale;
                }

                logger.LogError(ex, "Cloudflare TURN unavailable and nothing cached; this call has STUN only");
                var stun = options.Value.Stun;
                return stun.Count > 0 ? [new IceServer(stun)] : [];
            }
        }
        finally
        {
            _refresh.Release();
        }
    }

    private async Task<IReadOnlyList<IceServer>> FetchAsync(CloudflareTurnOptions cloudflare, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{BaseUrl}/v1/turn/keys/{Uri.EscapeDataString(cloudflare.KeyId)}/credentials/generate-ice-servers")
        {
            Content = JsonContent.Create(new { ttl = cloudflare.TtlSeconds }),
        };
        request.Headers.Authorization = new("Bearer", cloudflare.ApiToken);
        using var response = await httpClients.CreateClient(HttpClientName).SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Cloudflare TURN returned {(int)response.StatusCode}.", null, response.StatusCode);
        }

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var entries = document.RootElement.GetProperty("iceServers");
        var servers = (entries.ValueKind == JsonValueKind.Array ? entries.EnumerateArray().ToList() : [entries]).Select(Parse).ToList();
        return servers.Count > 0 ? servers : throw new JsonException("Cloudflare TURN returned no ICE servers.");
    }

    // The WebRTC dictionary allows "urls" to be a string or an array, and Cloudflare uses both.
    private static IceServer Parse(JsonElement entry)
    {
        var urls = entry.GetProperty("urls");
        return new IceServer(
            urls.ValueKind == JsonValueKind.Array ? urls.EnumerateArray().Select(u => u.GetString()!).ToList() : [urls.GetString()!],
            entry.TryGetProperty("username", out var username) ? username.GetString() : null,
            entry.TryGetProperty("credential", out var credential) ? credential.GetString() : null);
    }
}
