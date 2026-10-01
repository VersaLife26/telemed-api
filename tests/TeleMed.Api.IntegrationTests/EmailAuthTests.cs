using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Auth;
using TeleMed.Application.Testing;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class EmailAuthTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private const string Password = "correct horse battery";

    [Fact]
    public async Task Register_then_login()
    {
        var client = Factory.CreateClient();

        var registered = await (await client.PostJsonAsync("/api/v1/auth/register/email",
            new { email = "Nimal@Example.com", password = Password, fullName = "Nimal Perera", language = "ta" })).ReadAsync<AuthResponse>();
        registered.User.Email.ShouldBe("nimal@example.com");
        registered.User.Role.ShouldBe(UserRole.Patient);
        registered.User.Language.ShouldBe(Language.Ta);
        registered.User.HasPassword.ShouldBeTrue();

        var login = await (await client.PostJsonAsync("/api/v1/auth/login/email",
            new { email = "NIMAL@example.com", password = Password })).ReadAsync<AuthResponse>();
        login.User.Id.ShouldBe(registered.User.Id);
    }

    [Fact]
    public async Task Duplicate_email_is_a_conflict()
    {
        var client = Factory.CreateClient();
        await client.PostJsonAsync("/api/v1/auth/register/email", new { email = "dup@example.com", password = Password, fullName = "A" });

        var second = await client.PostJsonAsync("/api/v1/auth/register/email", new { email = "DUP@example.com", password = Password, fullName = "B" });

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.ProblemCodeAsync()).ShouldBe("email_taken");
    }

    [Fact]
    public async Task Short_password_is_rejected()
    {
        var response = await Factory.CreateClient().PostJsonAsync("/api/v1/auth/register/email",
            new { email = "short@example.com", password = "abc", fullName = "A" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_look_the_same()
    {
        await Factory.CreateUserAsync(UserRole.Patient, "known@example.com", Password);
        var client = Factory.CreateClient();

        var wrong = await client.PostJsonAsync("/api/v1/auth/login/email", new { email = "known@example.com", password = "not the password" });
        var unknown = await client.PostJsonAsync("/api/v1/auth/login/email", new { email = "nobody@example.com", password = "not the password" });

        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknown.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await wrong.ProblemCodeAsync()).ShouldBe("invalid_credentials");
        (await unknown.ProblemCodeAsync()).ShouldBe("invalid_credentials");
    }

    [Fact]
    public async Task Five_failures_lock_the_account()
    {
        await Factory.CreateUserAsync(UserRole.Patient, "lock@example.com", Password);
        var client = Factory.CreateClient();

        for (var i = 0; i < 4; i++)
        {
            (await (await client.PostJsonAsync("/api/v1/auth/login/email", new { email = "lock@example.com", password = "wrong password" }))
                .ProblemCodeAsync()).ShouldBe("invalid_credentials");
        }

        var fifth = await client.PostJsonAsync("/api/v1/auth/login/email", new { email = "lock@example.com", password = "wrong password" });
        (await fifth.ProblemCodeAsync()).ShouldBe("locked_out");

        var correct = await client.PostJsonAsync("/api/v1/auth/login/email", new { email = "lock@example.com", password = Password });
        correct.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await correct.ProblemCodeAsync()).ShouldBe("locked_out");
    }

    [Fact]
    public async Task Forgot_password_sends_a_link_that_sets_a_new_password()
    {
        await Factory.CreateUserAsync(UserRole.Patient, "reset@example.com", Password);
        var client = Factory.CreateClient();

        (await client.PostJsonAsync("/api/v1/auth/password/forgot", new { email = "Reset@Example.com" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var token = await Factory.LatestResetTokenAsync("reset@example.com");
        var reset = await (await client.PostJsonAsync("/api/v1/auth/password/reset",
            new { token, newPassword = "a different password" })).ReadAsync<AuthResponse>();
        reset.User.Email.ShouldBe("reset@example.com");
        reset.User.HasPassword.ShouldBeTrue();

        (await client.PostJsonAsync("/api/v1/auth/login/email", new { email = "reset@example.com", password = Password }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await (await client.PostJsonAsync("/api/v1/auth/login/email",
            new { email = "reset@example.com", password = "a different password" })).ReadAsync<AuthResponse>())
            .User.Id.ShouldBe(reset.User.Id);

        (await client.PostJsonAsync("/api/v1/auth/password/reset", new { token, newPassword = "third password 1" }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forgot_password_does_not_reveal_whether_the_email_exists()
    {
        await Factory.CreateUserAsync(UserRole.Patient, "known-reset@example.com", Password);
        var client = Factory.CreateClient();

        var known = await client.PostJsonAsync("/api/v1/auth/password/forgot", new { email = "known-reset@example.com" });
        var unknown = await client.PostJsonAsync("/api/v1/auth/password/forgot", new { email = "nobody-reset@example.com" });

        known.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Doctor_reset_link_points_at_the_doctor_app()
    {
        await Factory.CreateUserAsync(UserRole.Doctor, "doc-reset@example.com", Password);
        var client = Factory.CreateClient();
        (await client.PostJsonAsync("/api/v1/auth/password/forgot", new { email = "doc-reset@example.com" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var message = await (await Factory.CreateTestInboxClient()
            .GetAsync("/api/v1/test/captured-messages/latest?recipient=doc-reset@example.com&channel=email", TestContext.Current.CancellationToken))
            .ReadAsync<CapturedMessageDto>();
        message.Body.ShouldContain("https://doctor.telemed.test/login/reset?token=");
    }

    [Fact]
    public async Task Expired_reset_token_is_rejected()
    {
        await Factory.CreateUserAsync(UserRole.Patient, "expire-reset@example.com", Password);
        var client = Factory.CreateClient();
        (await client.PostJsonAsync("/api/v1/auth/password/forgot", new { email = "expire-reset@example.com" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var token = await Factory.LatestResetTokenAsync("expire-reset@example.com");

        Factory.Time.Advance(TimeSpan.FromMinutes(31));
        var expired = await client.PostJsonAsync("/api/v1/auth/password/reset", new { token, newPassword = "a different password" });
        expired.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await expired.ProblemCodeAsync()).ShouldBe("invalid_reset_token");
    }

    [Fact]
    public async Task Fourth_reset_email_within_an_hour_is_rate_limited_per_address()
    {
        await Factory.CreateUserAsync(UserRole.Patient, "limit-reset@example.com", Password);
        var client = Factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            (await client.PostJsonAsync("/api/v1/auth/password/forgot", new { email = "limit-reset@example.com" }))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);
            Factory.Time.Advance(TimeSpan.FromMinutes(1));
        }

        var limited = await client.PostJsonAsync("/api/v1/auth/password/forgot", new { email = "limit-reset@example.com" });
        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        Factory.Time.Advance(TimeSpan.FromHours(1));
        (await client.PostJsonAsync("/api/v1/auth/password/forgot", new { email = "limit-reset@example.com" }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
