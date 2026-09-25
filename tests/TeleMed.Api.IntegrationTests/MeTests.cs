using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Auth;
using TeleMed.Application.Users;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class MeTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Me_requires_authentication()
    {
        (await Factory.CreateClient().GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_profile_with_optimistic_concurrency()
    {
        var auth = await Factory.SignInWithPhoneAsync();
        var client = Factory.CreateClient().WithBearer(auth.AccessToken);
        var me = await (await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).ReadAsync<MeDto>();

        var updated = await (await client.PutJsonAsync("/api/v1/me", new
        {
            fullName = "Sita Kumari",
            address = "12 Galle Road, Colombo",
            dateOfBirth = "1990-04-13",
            sex = "female",
            allergies = "Penicillin",
            language = "si",
            version = me.Version,
        })).ReadAsync<MeDto>();

        updated.FullName.ShouldBe("Sita Kumari");
        updated.DateOfBirth.ShouldBe(new DateOnly(1990, 4, 13));
        updated.Sex.ShouldBe(Sex.Female);
        updated.Language.ShouldBe(Language.Si);
        updated.Version.ShouldNotBe(me.Version);

        var stale = await client.PutJsonAsync("/api/v1/me", new { fullName = "Other", language = "en", version = me.Version });
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await stale.ProblemCodeAsync()).ShouldBe("concurrency_conflict");
    }

    [Fact]
    public async Task Future_date_of_birth_is_rejected()
    {
        var auth = await Factory.SignInWithPhoneAsync();
        var client = Factory.CreateClient().WithBearer(auth.AccessToken);

        var response = await client.PutJsonAsync("/api/v1/me", new
        {
            fullName = "A",
            dateOfBirth = DateOnly.FromDateTime(Factory.Time.GetUtcNow().UtcDateTime.AddDays(2)),
            language = "en",
            version = auth.User.Version,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Otp_user_can_set_a_password_then_change_it_with_the_current_one()
    {
        var auth = await Factory.SignInWithPhoneAsync();
        auth.User.HasPassword.ShouldBeFalse();
        var client = Factory.CreateClient().WithBearer(auth.AccessToken);

        var set = await (await client.PutJsonAsync("/api/v1/me/password", new { newPassword = "first password" })).ReadAsync<AuthResponse>();
        set.User.HasPassword.ShouldBeTrue();

        (await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Factory.CreateClient().PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var next = Factory.CreateClient().WithBearer(set.AccessToken);
        var wrong = await next.PutJsonAsync("/api/v1/me/password", new { currentPassword = "nope nope", newPassword = "second password" });
        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await next.PutJsonAsync("/api/v1/me/password", new { currentPassword = "first password", newPassword = "second password" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_schedules_erasure_and_ends_sessions()
    {
        var auth = await Factory.SignInWithPhoneAsync();
        var client = Factory.CreateClient().WithBearer(auth.AccessToken);

        (await client.DeleteAsync("/api/v1/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Factory.CreateClient().PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await using var connection = await Fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT status, erasure_due_at FROM users WHERE id = '{auth.User.Id}'";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        (await reader.ReadAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        reader.GetString(0).ShouldBe("deleted");
        reader.GetFieldValue<DateTimeOffset>(1).ShouldBe(Factory.Time.GetUtcNow().AddDays(30), TimeSpan.FromSeconds(1));
    }
}
