using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Auth;

namespace TeleMed.Api.IntegrationTests;

public class SessionTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Refresh_rotates_the_token()
    {
        var auth = await Factory.SignInWithPhoneAsync();

        var rotated = await (await Refresh(auth.RefreshToken)).ReadAsync<AuthResponse>();

        rotated.RefreshToken.ShouldNotBe(auth.RefreshToken);
        rotated.User.Id.ShouldBe(auth.User.Id);
        (await Factory.CreateClient().WithBearer(rotated.AccessToken).GetAsync("/api/v1/me", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_after_the_grace_period_revokes_the_family()
    {
        var auth = await Factory.SignInWithPhoneAsync();
        var rotated = await (await Refresh(auth.RefreshToken)).ReadAsync<AuthResponse>();

        Factory.Time.Advance(TimeSpan.FromSeconds(31));
        var reuse = await Refresh(auth.RefreshToken);
        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await reuse.ProblemCodeAsync()).ShouldBe("refresh_token_reused");

        var descendant = await Refresh(rotated.RefreshToken);
        descendant.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reuse_within_the_grace_period_is_tolerated()
    {
        var auth = await Factory.SignInWithPhoneAsync();
        await (await Refresh(auth.RefreshToken)).ReadAsync<AuthResponse>();

        Factory.Time.Advance(TimeSpan.FromSeconds(10));
        var parallel = await (await Refresh(auth.RefreshToken)).ReadAsync<AuthResponse>();

        (await Refresh(parallel.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var auth = await Factory.SignInWithPhoneAsync();

        Factory.Time.Advance(TimeSpan.FromDays(7) + TimeSpan.FromSeconds(1));

        var response = await Refresh(auth.RefreshToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ProblemCodeAsync()).ShouldBe("refresh_token_expired");
    }

    [Fact]
    public async Task Access_token_expires_after_fifteen_minutes()
    {
        var auth = await Factory.SignInWithPhoneAsync();
        var client = Factory.CreateClient().WithBearer(auth.AccessToken);

        Factory.Time.Advance(TimeSpan.FromMinutes(16));

        (await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_only_that_session()
    {
        var first = await Factory.SignInWithPhoneAsync();
        Factory.Time.Advance(TimeSpan.FromMinutes(1));
        var second = await Factory.SignInWithPhoneAsync();

        (await Factory.CreateClient().PostJsonAsync("/api/v1/auth/logout", new { refreshToken = first.RefreshToken }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await Refresh(first.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Refresh(second.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_all_revokes_refresh_tokens_and_live_access_tokens()
    {
        var auth = await Factory.SignInWithPhoneAsync();
        var client = Factory.CreateClient().WithBearer(auth.AccessToken);
        (await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.PostAsync("/api/v1/auth/logout-all", null, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Refresh(auth.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_all_requires_authentication()
    {
        var response = await Factory.CreateClient().PostAsync("/api/v1/auth/logout-all", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> Refresh(string refreshToken) =>
        Factory.CreateClient().PostJsonAsync("/api/v1/auth/refresh", new { refreshToken });
}
