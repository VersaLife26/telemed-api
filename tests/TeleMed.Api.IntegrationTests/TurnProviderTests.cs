using System.Net;
using System.Text;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Video;

namespace TeleMed.Api.IntegrationTests;

public class TurnProviderTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task None_serves_stun_only()
    {
        var servers = await Factory.Services.GetRequiredService<ITurnCredentialProvider>().GetIceServersAsync(Ct);

        servers.Single().Urls.ShouldBe(["stun:stun.cloudflare.com:3478"]);
        servers.Single().Username.ShouldBeNull();
    }

    [Fact]
    public async Task Static_adds_the_configured_turn_server()
    {
        await using var factory = Factory.WithSettings(
            ("Turn:Provider", "Static"),
            ("Turn:StaticUrls:0", "turn:turn.example.com:3478?transport=udp"),
            ("Turn:StaticUrls:1", "turns:turn.example.com:5349"),
            ("Turn:Username", "telemed"),
            ("Turn:Credential", "secret"));

        var servers = await factory.Services.GetRequiredService<ITurnCredentialProvider>().GetIceServersAsync(Ct);

        servers.Count.ShouldBe(2);
        servers[1].Urls.ShouldBe(["turn:turn.example.com:3478?transport=udp", "turns:turn.example.com:5349"]);
        (servers[1].Username, servers[1].Credential).ShouldBe(("telemed", "secret"));
    }

    [Fact]
    public async Task Cloudflare_mints_credentials_caches_them_and_falls_back_to_the_last_ones()
    {
        var cloudflare = new FakeCloudflare();
        await using var factory = Factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Turn:Provider"] = "Cloudflare",
                ["Turn:Cloudflare:KeyId"] = "key-id",
                ["Turn:Cloudflare:ApiToken"] = "api-token",
                ["Turn:Cloudflare:TtlSeconds"] = "3600",
            }));
            b.ConfigureTestServices(s => s.AddHttpClient(CloudflareTurnProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => cloudflare));
        });
        var provider = factory.Services.GetRequiredService<ITurnCredentialProvider>();
        provider.ShouldBeOfType<CloudflareTurnProvider>();

        var minted = await provider.GetIceServersAsync(Ct);
        await provider.GetIceServersAsync(Ct);

        cloudflare.Calls.ShouldBe(1);
        cloudflare.LastUri.ShouldBe("https://rtc.live.cloudflare.com/v1/turn/keys/key-id/credentials/generate-ice-servers");
        cloudflare.LastAuthorization.ShouldBe("Bearer api-token");
        cloudflare.LastBody.ShouldBe("""{"ttl":3600}""");
        minted.Count.ShouldBe(2);
        minted[0].Urls.ShouldBe(["stun:stun.cloudflare.com:3478", "turn:turn.cloudflare.com:3478?transport=udp"]);
        (minted[0].Username, minted[0].Credential).ShouldBe(("cf-user", "cf-credential"));
        minted[1].Urls.ShouldBe(["turns:turn.cloudflare.com:443?transport=tcp"]);

        Factory.Time.Advance(TimeSpan.FromMinutes(31));
        cloudflare.Fail = true;
        var stale = await provider.GetIceServersAsync(Ct);

        cloudflare.Calls.ShouldBe(2);
        stale.ShouldBeSameAs(minted);
    }

    private sealed class FakeCloudflare : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public string? LastUri { get; private set; }
        public string? LastAuthorization { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri!.ToString();
            LastAuthorization = request.Headers.Authorization?.ToString();
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (Fail)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            const string body = """
                {"iceServers":[
                  {"urls":["stun:stun.cloudflare.com:3478","turn:turn.cloudflare.com:3478?transport=udp"],"username":"cf-user","credential":"cf-credential"},
                  {"urls":"turns:turn.cloudflare.com:443?transport=tcp","username":"cf-user","credential":"cf-credential"}
                ]}
                """;
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
