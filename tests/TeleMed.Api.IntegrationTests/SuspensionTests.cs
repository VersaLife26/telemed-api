using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class SuspensionTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Suspended_user_cannot_log_in_refresh_or_use_a_live_access_token()
    {
        var auth = await Factory.CreateUserAsync(UserRole.Patient, "suspended@example.com", "a good password");
        var client = Factory.CreateClient().WithBearer(auth.AccessToken);
        (await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await Fixture.ExecuteSqlAsync($"UPDATE users SET status = 'suspended' WHERE id = '{auth.User.Id}'");
        Factory.Time.Advance(TimeSpan.FromSeconds(31));

        (await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var login = await Factory.CreateClient().PostJsonAsync("/api/v1/auth/login/email",
            new { email = "suspended@example.com", password = "a good password" });
        login.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await login.ProblemCodeAsync()).ShouldBe("account_suspended");

        var refresh = await Factory.CreateClient().PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken });
        refresh.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Suspended_user_cannot_sign_in_with_otp()
    {
        var auth = await Factory.SignInWithPhoneAsync("+94701234567");
        await Fixture.ExecuteSqlAsync($"UPDATE users SET status = 'suspended' WHERE id = '{auth.User.Id}'");
        var client = Factory.CreateClient();

        Factory.Time.Advance(TimeSpan.FromMinutes(1));
        await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone = "+94701234567" });
        var code = await Factory.LatestCodeAsync("+94701234567");
        var verify = await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone = "+94701234567", code });

        verify.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
