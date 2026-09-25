using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.AdminUsers;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class AdminUsersTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private const string Url = "/api/v1/admin/admin-users";

    [Fact]
    public async Task Super_admin_creates_lists_and_updates_admins()
    {
        var super = await Factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var client = Factory.CreateAdminClient(Factory.LocalAdminToken(super.Email));

        var created = await (await client.PostJsonAsync(Url, new { email = " Kasun@Example.com ", displayName = "Kasun", role = "finance" }))
            .ReadAsync<AdminUserDto>(HttpStatusCode.Created);
        var updated = await (await client.PatchJsonAsync($"{Url}/{created.Id}", new { displayName = "Kasun P", role = "ops" }))
            .ReadAsync<AdminUserDto>();
        var list = await (await client.GetAsync(Url, TestContext.Current.CancellationToken)).ReadAsync<List<AdminUserDto>>();

        created.Email.ShouldBe("kasun@example.com");
        created.CreatedByAdminId.ShouldBe(super.Id);
        created.IsActive.ShouldBeTrue();
        updated.DisplayName.ShouldBe("Kasun P");
        updated.Role.ShouldBe(AdminRole.Ops);
        list.Select(a => a.Id).ShouldBe([created.Id, super.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Duplicate_email_is_a_conflict()
    {
        var client = await Factory.AdminClientAsync(AdminRole.SuperAdmin);
        await client.PostJsonAsync(Url, new { email = "dup@example.com", displayName = "A", role = "support" });

        var second = await client.PostJsonAsync(Url, new { email = "DUP@example.com", displayName = "B", role = "support" });

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.ProblemCodeAsync()).ShouldBe("admin_email_taken");
    }

    [Fact]
    public async Task Invalid_requests_are_rejected()
    {
        var client = await Factory.AdminClientAsync(AdminRole.SuperAdmin);

        (await client.PostJsonAsync(Url, new { email = "not-an-email", displayName = "A", role = "support" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostJsonAsync(Url, new { email = "a@example.com", displayName = "", role = "support" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PatchJsonAsync($"{Url}/{Guid.NewGuid()}", new { displayName = "X" }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("demote")]
    [InlineData("deactivate")]
    [InlineData("patch-deactivate")]
    public async Task Admins_cannot_change_their_own_access(string change)
    {
        await Factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var self = await Factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var client = Factory.CreateAdminClient(Factory.LocalAdminToken(self.Email));

        var response = change switch
        {
            "demote" => await client.PatchJsonAsync($"{Url}/{self.Id}", new { role = "admin" }),
            "deactivate" => await client.PostJsonAsync($"{Url}/{self.Id}/deactivate", new { }),
            _ => await client.PatchJsonAsync($"{Url}/{self.Id}", new { isActive = false }),
        };

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ProblemCodeAsync()).ShouldBe("self_access_change");
        (await client.PatchJsonAsync($"{Url}/{self.Id}", new { displayName = "Still me" })).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_last_active_super_admin_cannot_be_removed()
    {
        var first = await Factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var second = await Factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var client = Factory.CreateAdminClient(Factory.LocalAdminToken(first.Email));
        (await client.GetAsync("/api/v1/admin/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Deactivate the caller behind the cache's back: a stale session must still not be able to remove the last super admin.
        await Fixture.ExecuteSqlAsync($"UPDATE admin_users SET is_active = false WHERE id = '{first.Id}'");

        var demote = await client.PatchJsonAsync($"{Url}/{second.Id}", new { role = "admin" });
        var deactivate = await client.PostJsonAsync($"{Url}/{second.Id}/deactivate", new { });

        demote.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await demote.ProblemCodeAsync()).ShouldBe("last_super_admin");
        deactivate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Fixture.ScalarAsync<string>($"SELECT role FROM admin_users WHERE id = '{second.Id}'")).ShouldBe("super_admin");
    }

    [Fact]
    public async Task Another_super_admin_can_be_demoted_while_one_remains()
    {
        var first = await Factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var second = await Factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var client = Factory.CreateAdminClient(Factory.LocalAdminToken(first.Email));

        var demoted = await (await client.PatchJsonAsync($"{Url}/{second.Id}", new { role = "admin" })).ReadAsync<AdminUserDto>();

        demoted.Role.ShouldBe(AdminRole.Admin);
    }

    [Fact]
    public async Task Bootstrap_creates_the_first_super_admin_once()
    {
        await using (var factory = Factory.WithSettings(("AdminAuth:BootstrapSuperAdminEmail", " Root@Example.com ")))
        {
            factory.CreateClient();
        }

        await using (var again = Factory.WithSettings(("AdminAuth:BootstrapSuperAdminEmail", "other@example.com")))
        {
            again.CreateClient();
        }

        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM admin_users")).ShouldBe(1);
        (await Fixture.ScalarAsync<string>("SELECT email || '/' || role || '/' || is_active FROM admin_users")).ShouldBe("root@example.com/super_admin/true");
        (await Fixture.ScalarAsync<string>("SELECT actor_type || '/' || action FROM audit_logs WHERE entity_type = 'admin_users'"))
            .ShouldBe("system/created");
    }

    [Fact]
    public async Task Bootstrap_does_nothing_when_admins_exist()
    {
        await Factory.CreateAdminAsync(AdminRole.Support);

        await using (var factory = Factory.WithSettings(("AdminAuth:BootstrapSuperAdminEmail", "root@example.com")))
        {
            factory.CreateClient();
        }

        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM admin_users WHERE role = 'super_admin'")).ShouldBe(0);
    }
}
