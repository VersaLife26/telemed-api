using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Auth;
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
}
