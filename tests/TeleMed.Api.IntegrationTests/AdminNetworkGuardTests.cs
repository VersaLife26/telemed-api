using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class AdminNetworkGuardTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private const string ClientIp = "203.0.113.7";

    [Fact]
    public async Task Empty_allowlist_denies_every_admin_request()
    {
        await using var factory = FromIp(allowAnyIp: false);
        var admin = await factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var client = factory.CreateAdminClient(Factory.LocalAdminToken(admin.Email));

        var me = await client.GetAsync("/api/v1/admin/me", TestContext.Current.CancellationToken);
        var anonymous = await factory.CreateClient().GetAsync("/api/v1/admin/permissions", TestContext.Current.CancellationToken);
        var publicRoute = await factory.CreateClient().GetAsync("/api/v1/specialties", TestContext.Current.CancellationToken);

        me.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await me.ProblemCodeAsync()).ShouldBe("ip_not_allowed");
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        publicRoute.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("203.0.113.0/24", HttpStatusCode.OK)]
    [InlineData(ClientIp, HttpStatusCode.OK)]
    [InlineData("10.0.0.0/8", HttpStatusCode.Forbidden)]
    public async Task Allowlist_admits_only_matching_networks(string entry, HttpStatusCode expected)
    {
        await using var factory = FromIp(allowAnyIp: false, ("AdminAuth:IpAllowlist:0", entry));
        var admin = await factory.CreateAdminAsync(AdminRole.SuperAdmin);

        var response = await factory.CreateAdminClient(Factory.LocalAdminToken(admin.Email))
            .GetAsync("/api/v1/admin/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, null, HttpStatusCode.Forbidden)]
    [InlineData("https://evil.example", null, HttpStatusCode.Forbidden)]
    [InlineData("https://evil.example", "cross-site", HttpStatusCode.Forbidden)]
    [InlineData(TeleMedApiFactory.AdminOrigin, null, HttpStatusCode.Created)]
    [InlineData(null, "same-origin", HttpStatusCode.Created)]
    public async Task Unsafe_admin_requests_need_the_admin_origin(string? origin, string? fetchSite, HttpStatusCode expected)
    {
        var admin = await Factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var client = Factory.CreateClient().WithBearer(Factory.LocalAdminToken(admin.Email));
        if (origin is not null)
        {
            client.DefaultRequestHeaders.Add("Origin", origin);
        }

        if (fetchSite is not null)
        {
            client.DefaultRequestHeaders.Add("Sec-Fetch-Site", fetchSite);
        }

        var get = await client.GetAsync("/api/v1/admin/me", TestContext.Current.CancellationToken);
        var post = await client.PostJsonAsync("/api/v1/admin/admin-users",
            new { email = $"{Guid.NewGuid():N}@admin.test", displayName = "X", role = "support" });

        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        post.StatusCode.ShouldBe(expected);
        if (expected == HttpStatusCode.Forbidden)
        {
            (await post.ProblemCodeAsync()).ShouldBe("origin_not_allowed");
        }
    }

    [Fact]
    public async Task Admin_cors_allows_credentials_only_for_admin_origins()
    {
        await using var factory = Factory.WithSettings(("Cors:Origins:0", "https://app.telemed.test"));

        var admin = await Preflight(factory, "/api/v1/admin/admin-users", TeleMedApiFactory.AdminOrigin);
        var consumerOnAdmin = await Preflight(factory, "/api/v1/admin/admin-users", "https://app.telemed.test");
        var consumer = await Preflight(factory, "/api/v1/me", "https://app.telemed.test");

        admin.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([TeleMedApiFactory.AdminOrigin]);
        admin.Headers.GetValues("Access-Control-Allow-Credentials").ShouldBe(["true"]);
        consumerOnAdmin.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
        consumer.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(["https://app.telemed.test"]);
        consumer.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
    }

    private static async Task<HttpResponseMessage> Preflight(WebApplicationFactory<Program> factory, string url, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, url);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
    }

    private WebApplicationFactory<Program> FromIp(bool allowAnyIp, params (string Key, string Value)[] settings) =>
        Factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(
                settings.Append(("AdminAuth:AllowAnyIp", allowAnyIp.ToString())).Select(s => KeyValuePair.Create(s.Item1, (string?)s.Item2))));
            b.ConfigureTestServices(s => s.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(IPAddress.Parse(ClientIp))));
        });

    private sealed class RemoteIpStartupFilter(IPAddress ip) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = ip;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
