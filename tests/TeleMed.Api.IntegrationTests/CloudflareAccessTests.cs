using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class CloudflareAccessTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private const string Team = "versalife.cloudflareaccess.com";
    private const string Audience = "cf-access-aud-tag";

    private static readonly RSA SigningKey = RSA.Create(2048);

    [Fact]
    public async Task Header_or_cookie_assertion_authenticates_an_admin()
    {
        await using var factory = CloudflareFactory();
        var admin = await factory.CreateAdminAsync(AdminRole.Support);
        var token = CloudflareToken(admin.Email);

        var header = factory.CreateClient();
        header.DefaultRequestHeaders.Add("Cf-Access-Jwt-Assertion", token);
        var cookie = factory.CreateClient();
        cookie.DefaultRequestHeaders.Add("Cookie", $"CF_Authorization={token}");

        (await header.GetAsync("/api/v1/admin/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cookie.GetAsync("/api/v1/admin/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("https://someone-else.cloudflareaccess.com", Audience)]
    [InlineData("https://" + Team, "another-app")]
    public async Task Wrong_issuer_or_audience_is_rejected(string issuer, string audience)
    {
        await using var factory = CloudflareFactory();
        var admin = await factory.CreateAdminAsync(AdminRole.Support);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cf-Access-Jwt-Assertion", CloudflareToken(admin.Email, issuer, audience));

        (await client.GetAsync("/api/v1/admin/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cloudflare_assertions_do_not_authenticate_user_routes()
    {
        await using var factory = CloudflareFactory();
        var admin = await factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cf-Access-Jwt-Assertion", CloudflareToken(admin.Email));

        (await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private WebApplicationFactory<Program> CloudflareFactory() => Factory.WithWebHostBuilder(b =>
    {
        b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminAuth:CloudflareAccess:Enabled"] = "true",
            ["AdminAuth:CloudflareAccess:TeamDomain"] = Team,
            ["AdminAuth:CloudflareAccess:Audience"] = Audience,
            ["AdminAuth:LocalJwt:Enabled"] = "false",
        }));
        b.ConfigureTestServices(s => s.AddHttpClient("cloudflare-access").ConfigurePrimaryHttpMessageHandler(() => new JwksHandler()));
    });

    private string CloudflareToken(string email, string issuer = "https://" + Team, string audience = Audience)
    {
        var now = Factory.Time.GetUtcNow().UtcDateTime;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddHours(1),
            Claims = new Dictionary<string, object> { ["email"] = email, ["sub"] = Guid.NewGuid().ToString() },
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(SigningKey) { KeyId = "test-key" }, SecurityAlgorithms.RsaSha256),
        });
    }

    private sealed class JwksHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri.ShouldBe(new Uri($"https://{Team}/cdn-cgi/access/certs"));
            var key = SigningKey.ExportParameters(includePrivateParameters: false);
            var jwks = JsonSerializer.Serialize(new
            {
                keys = new[]
                {
                    new { kty = "RSA", kid = "test-key", use = "sig", alg = "RS256", n = Base64UrlEncoder.Encode(key.Modulus), e = Base64UrlEncoder.Encode(key.Exponent) },
                },
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(jwks) });
        }
    }
}
