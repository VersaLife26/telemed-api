using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Auth;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Identity;

namespace TeleMed.Api.IntegrationTests;

public class GoogleAuthTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task First_sign_in_creates_a_patient_and_later_sign_ins_reuse_it()
    {
        var client = Factory.CreateClient();
        var token = FakeGoogleTokenValidator.Token("google-sub-1", "Kamala@Gmail.com", name: "Kamala");

        var first = await (await client.PostJsonAsync("/api/v1/auth/google", new { idToken = token })).ReadAsync<AuthResponse>();
        var second = await (await client.PostJsonAsync("/api/v1/auth/google", new { idToken = token })).ReadAsync<AuthResponse>();

        first.User.Role.ShouldBe(UserRole.Patient);
        first.User.FullName.ShouldBe("Kamala");
        first.User.Email.ShouldBe("kamala@gmail.com");
        second.User.Id.ShouldBe(first.User.Id);
    }

    [Fact]
    public async Task Verified_email_links_to_an_existing_account()
    {
        var existing = await Factory.CreateUserAsync(UserRole.Doctor, "doc@example.com", "doctor password");

        var linked = await (await Factory.CreateClient().PostJsonAsync("/api/v1/auth/google",
            new { idToken = FakeGoogleTokenValidator.Token("google-sub-2", "doc@example.com") })).ReadAsync<AuthResponse>();

        linked.User.Id.ShouldBe(existing.User.Id);
        linked.User.Role.ShouldBe(UserRole.Doctor);
    }

    [Fact]
    public async Task Linking_to_an_unconfirmed_email_account_evicts_the_previous_credentials()
    {
        var client = Factory.CreateClient();
        var registered = await (await client.PostJsonAsync("/api/v1/auth/register/email",
            new { email = "squatter@example.com", password = "squatter password", fullName = "Squatter" })).ReadAsync<AuthResponse>();

        var linked = await (await client.PostJsonAsync("/api/v1/auth/google",
            new { idToken = FakeGoogleTokenValidator.Token("google-sub-owner", "squatter@example.com") })).ReadAsync<AuthResponse>();

        linked.User.Id.ShouldBe(registered.User.Id);
        linked.User.HasPassword.ShouldBeFalse();
        (await client.PostJsonAsync("/api/v1/auth/login/email", new { email = "squatter@example.com", password = "squatter password" }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = registered.RefreshToken }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Factory.CreateClient().WithBearer(registered.AccessToken).GetAsync("/api/v1/me", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Factory.CreateClient().WithBearer(linked.AccessToken).GetAsync("/api/v1/me", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Linking_to_a_confirmed_email_account_keeps_its_password()
    {
        var client = Factory.CreateClient();
        var first = await (await client.PostJsonAsync("/api/v1/auth/google",
            new { idToken = FakeGoogleTokenValidator.Token("google-sub-a", "owner@example.com") })).ReadAsync<AuthResponse>();
        (await Factory.CreateClient().WithBearer(first.AccessToken).PutJsonAsync("/api/v1/me/password", new { newPassword = "owner password" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var second = await (await client.PostJsonAsync("/api/v1/auth/google",
            new { idToken = FakeGoogleTokenValidator.Token("google-sub-b", "owner@example.com") })).ReadAsync<AuthResponse>();

        second.User.Id.ShouldBe(first.User.Id);
        second.User.HasPassword.ShouldBeTrue();
        (await client.PostJsonAsync("/api/v1/auth/login/email", new { email = "owner@example.com", password = "owner password" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unverified_email_and_invalid_tokens_are_rejected()
    {
        var client = Factory.CreateClient();

        var unverified = await client.PostJsonAsync("/api/v1/auth/google",
            new { idToken = FakeGoogleTokenValidator.Token("google-sub-3", "x@example.com", verified: false) });
        var invalid = await client.PostJsonAsync("/api/v1/auth/google", new { idToken = "garbage" });

        unverified.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        invalid.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoint_is_404_when_no_client_ids_are_configured()
    {
        await using var factory = Factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>()));

        var response = await factory.CreateClient().PostJsonAsync("/api/v1/auth/google", new { idToken = "anything" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
