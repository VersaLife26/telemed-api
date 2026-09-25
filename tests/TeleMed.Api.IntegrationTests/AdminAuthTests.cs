using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.AdminUsers;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class AdminAuthTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private const string AdminMe = "/api/v1/admin/me";

    [Fact]
    public async Task Admin_routes_require_an_admin_token()
    {
        var response = await Factory.CreateClient().GetAsync(AdminMe, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.Patient)]
    [InlineData(UserRole.Doctor)]
    public async Task User_tokens_do_not_authenticate_admin_routes(UserRole role)
    {
        var user = await Factory.CreateUserAsync(role);

        var response = await Factory.CreateAdminClient(user.AccessToken).GetAsync(AdminMe, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_tokens_do_not_authenticate_user_routes()
    {
        var admin = await Factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var client = Factory.CreateAdminClient(Factory.LocalAdminToken(admin.Email));

        (await client.GetAsync(AdminMe, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Tokens_signed_with_another_key_are_rejected()
    {
        var admin = await Factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var token = Factory.LocalAdminToken(admin.Email, "some-other-signing-key-that-is-long-enough-0123");

        var response = await Factory.CreateAdminClient(token).GetAsync(AdminMe, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unknown_email_is_forbidden()
    {
        var response = await Factory.CreateAdminClient(Factory.LocalAdminToken($"{Guid.NewGuid():N}@nobody.test"))
            .GetAsync(AdminMe, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Inactive_admin_is_forbidden()
    {
        var admin = await Factory.CreateAdminAsync(AdminRole.SuperAdmin, isActive: false);

        var response = await Factory.CreateAdminClient(Factory.LocalAdminToken(admin.Email))
            .GetAsync(AdminMe, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Me_reflects_the_admin_row_and_email_matching_is_case_insensitive()
    {
        var admin = await Factory.CreateAdminAsync(AdminRole.Ops);

        var me = await (await Factory.CreateAdminClient(Factory.LocalAdminToken(admin.Email.ToUpperInvariant()))
            .GetAsync(AdminMe, TestContext.Current.CancellationToken)).ReadAsync<AdminMeDto>();

        me.Id.ShouldBe(admin.Id);
        me.Email.ShouldBe(admin.Email);
        me.DisplayName.ShouldBe("Test Ops");
        me.Role.ShouldBe(AdminRole.Ops);
        (await Fixture.ScalarAsync<DateTime?>($"SELECT last_login_at FROM admin_users WHERE id = '{admin.Id}'")).ShouldNotBeNull();
    }

    [Fact]
    public async Task Deactivation_takes_effect_on_the_next_request()
    {
        var super = await Factory.AdminClientAsync(AdminRole.SuperAdmin);
        var victim = await Factory.CreateAdminAsync(AdminRole.Support);
        var victimClient = Factory.CreateAdminClient(Factory.LocalAdminToken(victim.Email));
        (await victimClient.GetAsync(AdminMe, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await super.PostJsonAsync($"/api/v1/admin/admin-users/{victim.Id}/deactivate", new { })).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await victimClient.GetAsync(AdminMe, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
