using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;

namespace TeleMed.Api.IntegrationTests;

public class RateLimitingTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Login_is_limited_per_client_ip()
    {
        await using var factory = Factory.WithSettings(("RateLimiting:Login:PermitLimit", "2"));
        var client = factory.CreateClient();
        var body = new { email = "nobody@example.com", password = "whatever password" };

        (await client.PostJsonAsync("/api/v1/auth/login/email", body)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.PostJsonAsync("/api/v1/auth/login/email", body)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var limited = await client.PostJsonAsync("/api/v1/auth/login/email", body);

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData("Eligibility", "/api/v1/doctor-applications/eligibility?phone=0771234567")]
    [InlineData("Slots", "/api/v1/doctors/00000000-0000-0000-0000-000000000001/slots")]
    public async Task Public_lookups_are_limited_per_client_ip(string policy, string url)
    {
        await using var factory = Factory.WithSettings(($"RateLimiting:{policy}:PermitLimit", "1"));
        var client = factory.CreateClient();

        (await client.GetAsync(url, TestContext.Current.CancellationToken)).StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        (await client.GetAsync(url, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
