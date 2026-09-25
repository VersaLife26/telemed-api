using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Auth;
using TeleMed.Application.Testing;
using TeleMed.Application.Users;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class OtpAuthTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private const string Phone = "+94771234567";

    [Fact]
    public async Task Otp_flow_via_capture_inbox_issues_tokens_that_work_on_me()
    {
        var client = Factory.CreateClient();

        var sent = await (await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone = "077 123 4567" })).ReadAsync<OtpSentResponse>();
        sent.Channel.ShouldBe(MessageChannel.Sms);
        sent.ExpiresIn.ShouldBe(300);

        var code = await Factory.LatestCodeAsync(Phone);
        code.Length.ShouldBe(6);

        var auth = await (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone = "0771234567", code })).ReadAsync<AuthResponse>();
        auth.AccessToken.ShouldNotBeNullOrEmpty();
        auth.RefreshToken.ShouldNotBeNullOrEmpty();
        auth.ExpiresIn.ShouldBe(900);
        auth.User.PhoneNumber.ShouldBe(Phone);
        auth.User.Role.ShouldBe(UserRole.Patient);

        var me = await (await Factory.CreateClient().WithBearer(auth.AccessToken)
            .GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).ReadAsync<MeDto>();
        me.Id.ShouldBe(auth.User.Id);
    }

    [Fact]
    public async Task Verifying_again_signs_into_the_same_account()
    {
        var first = await Factory.SignInWithPhoneAsync(Phone);
        Factory.Time.Advance(TimeSpan.FromMinutes(1));

        var second = await Factory.SignInWithPhoneAsync(Phone);

        second.User.Id.ShouldBe(first.User.Id);
    }

    [Fact]
    public async Task Email_otp_uses_the_email_channel_and_the_requested_language()
    {
        var client = Factory.CreateClient();

        await (await client.PostJsonAsync("/api/v1/auth/otp/send", new { email = "Ada@Example.com", language = "si" })).ReadAsync<OtpSentResponse>();

        var message = await (await Factory.CreateTestInboxClient()
            .GetAsync("/api/v1/test/captured-messages/latest?recipient=ada@example.com&channel=email", TestContext.Current.CancellationToken))
            .ReadAsync<CapturedMessageDto>();
        message.Subject.ShouldNotBeNull();
        message.Body.ShouldContain("කේතය");

        var code = await Factory.LatestCodeAsync("ada@example.com");
        var auth = await (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { email = "ada@example.com", code })).ReadAsync<AuthResponse>();
        auth.User.Email.ShouldBe("ada@example.com");
    }

    [Fact]
    public async Task Fourth_send_within_an_hour_is_rate_limited_per_destination()
    {
        var client = Factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            (await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone = Phone })).StatusCode.ShouldBe(HttpStatusCode.OK);
            Factory.Time.Advance(TimeSpan.FromMinutes(1));
        }

        var limited = await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone = Phone });
        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        Factory.Time.Advance(TimeSpan.FromHours(1));
        (await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone = Phone })).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Code_is_burned_after_five_wrong_attempts()
    {
        var client = Factory.CreateClient();
        await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone = Phone });
        var code = await Factory.LatestCodeAsync(Phone);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone = Phone, code = wrong });
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await response.ProblemCodeAsync()).ShouldBe("otp_invalid");
        }

        var correct = await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone = Phone, code });
        correct.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Code_expires_after_five_minutes_and_is_single_use()
    {
        var client = Factory.CreateClient();
        await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone = Phone });
        var code = await Factory.LatestCodeAsync(Phone);

        (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone = Phone, code })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone = Phone, code })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        Factory.Time.Advance(TimeSpan.FromMinutes(1));
        await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone = Phone });
        var second = await Factory.LatestCodeAsync(Phone);
        Factory.Time.Advance(TimeSpan.FromMinutes(5));
        (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone = Phone, code = second })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Fixed_code_switch_makes_every_code_predictable()
    {
        await using var factory = Factory.WithSettings(("Otp:FixedCode", "424242"));
        var client = factory.CreateClient();

        await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone = Phone });
        var auth = await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone = Phone, code = "424242" });

        auth.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("0112345678", null)]
    [InlineData(null, "not-an-email")]
    [InlineData("0771234567", "a@example.com")]
    [InlineData(null, null)]
    public async Task Invalid_destinations_are_rejected(string? phone, string? email)
    {
        var response = await Factory.CreateClient().PostJsonAsync("/api/v1/auth/otp/send", new { phone, email });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
