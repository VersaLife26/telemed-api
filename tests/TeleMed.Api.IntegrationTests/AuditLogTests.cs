using System.Net;
using System.Text.Json;
using Npgsql;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.AdminUsers;
using TeleMed.Application.Admin.Content;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class AuditLogTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Creating_an_admin_user_records_actor_and_new_values()
    {
        var super = await Factory.CreateAdminAsync(AdminRole.SuperAdmin);
        var client = Factory.CreateAdminClient(Factory.LocalAdminToken(super.Email));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("audit-test/1.0");

        var created = await (await client.PostJsonAsync("/api/v1/admin/admin-users",
            new { email = "new@example.com", displayName = "New Admin", role = "finance" })).ReadAsync<AdminUserDto>(HttpStatusCode.Created);

        var row = await SingleRowAsync($"entity_type = 'admin_users' AND entity_id = '{created.Id}'");
        row.ActorType.ShouldBe("admin");
        row.ActorId.ShouldBe(super.Id);
        row.ActorEmail.ShouldBe(super.Email);
        row.Action.ShouldBe("created");
        row.UserAgent.ShouldBe("audit-test/1.0");
        row.RequestId.ShouldNotBeNullOrEmpty();
        row.Changes.GetProperty("email").GetProperty("new").GetString().ShouldBe("new@example.com");
        row.Changes.GetProperty("role").GetProperty("new").GetString().ShouldBe("finance");
        row.Changes.GetProperty("created_by_admin_id").GetProperty("new").GetGuid().ShouldBe(super.Id);
        row.Changes.TryGetProperty("created_at", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Updating_a_specialty_records_only_changed_columns()
    {
        var admin = await Factory.CreateAdminAsync(AdminRole.Admin);
        var client = Factory.CreateAdminClient(Factory.LocalAdminToken(admin.Email));

        var current = await (await client.GetAsync("/api/v1/admin/specialties/general_practice", TestContext.Current.CancellationToken))
            .ReadAsync<AdminSpecialtyDto>();

        (await client.PutJsonAsync("/api/v1/admin/specialties/general_practice", current with { NameEn = "General Practice" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var row = await SingleRowAsync("entity_type = 'specialties' AND entity_id = 'general_practice'");
        row.ActorType.ShouldBe("admin");
        row.ActorId.ShouldBe(admin.Id);
        row.Action.ShouldBe("updated");
        row.Changes.EnumerateObject().Select(p => p.Name).ShouldBe(["name_en"]);
        row.Changes.GetProperty("name_en").GetProperty("old").GetString().ShouldBe("General Practitioner");
        row.Changes.GetProperty("name_en").GetProperty("new").GetString().ShouldBe("General Practice");
    }

    [Fact]
    public async Task Sign_in_bookkeeping_is_not_audited()
    {
        var admin = await Factory.CreateAdminAsync(AdminRole.Support);

        (await Factory.CreateAdminClient(Factory.LocalAdminToken(admin.Email)).GetAsync("/api/v1/admin/me", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM audit_logs WHERE action = 'updated'")).ShouldBe(0);
    }

    [Theory]
    [InlineData("UPDATE audit_logs SET action = 'tampered'")]
    [InlineData("DELETE FROM audit_logs")]
    [InlineData("TRUNCATE audit_logs")]
    public async Task Audit_rows_are_append_only(string sql)
    {
        await Factory.CreateAdminAsync(AdminRole.Support);

        var ex = await Should.ThrowAsync<PostgresException>(() => Fixture.ExecuteSqlAsync(sql));

        ex.MessageText.ShouldContain("append-only");
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM audit_logs WHERE action = 'created'")).ShouldBe(1);
    }

    private sealed record AuditRow(string ActorType, Guid? ActorId, string? ActorEmail, string Action, JsonElement Changes, string? UserAgent, string? RequestId);

    private async Task<AuditRow> SingleRowAsync(string where)
    {
        await using var connection = await Fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT actor_type, actor_id, actor_email, action, changes::text, user_agent, request_id FROM audit_logs WHERE {where}";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        (await reader.ReadAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
        var row = new AuditRow(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            JsonDocument.Parse(reader.GetString(4)).RootElement,
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6));
        (await reader.ReadAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        return row;
    }
}
