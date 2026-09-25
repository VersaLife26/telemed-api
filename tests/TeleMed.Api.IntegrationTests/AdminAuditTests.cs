using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.Audit;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class AdminAuditTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Audit_is_filterable_and_only_finance_and_super_admins_export_it()
    {
        var superAdmin = await Factory.AdminClientAsync(AdminRole.SuperAdmin);
        var created = await (await superAdmin.PostJsonAsync("/api/v1/admin/drugs",
                new { name = "Auditol", genericName = "Auditamine", strength = "5mg", form = "tablet", isControlled = false, isGeneric = true }))
            .ReadAsync<TeleMed.Application.Admin.Content.AdminDrugDto>(HttpStatusCode.Created);
        (await superAdmin.PutJsonAsync($"/api/v1/admin/drugs/{created.Id}",
            new { name = "Auditol", genericName = "Auditamine", strength = "10mg", form = "tablet", isControlled = false, isGeneric = true })).EnsureSuccessStatusCode();

        var support = await Factory.AdminClientAsync(AdminRole.Support);
        var page = await (await support.GetAsync($"/api/v1/admin/audit?entityType=drugs&entityId={created.Id}", Ct)).ReadAsync<PagedResult<AuditEntryDto>>();
        page.Total.ShouldBe(2);
        page.Items.Select(a => a.Action).ShouldBe(["updated", "created"]);
        page.Items.ShouldAllBe(a => a.ActorType == AuditActorType.Admin);
        var actorId = page.Items[0].ActorId!.Value;
        (await (await support.GetAsync($"/api/v1/admin/audit?actorId={actorId}&pageSize=1", Ct)).ReadAsync<PagedResult<AuditEntryDto>>()).Items.Count.ShouldBe(1);
        var future = Uri.EscapeDataString(Factory.Time.GetUtcNow().AddMinutes(1).ToString("O"));
        (await (await support.GetAsync($"/api/v1/admin/audit?from={future}", Ct)).ReadAsync<PagedResult<AuditEntryDto>>()).Total.ShouldBe(0);

        (await support.GetAsync("/api/v1/admin/audit.csv", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await (await Factory.AdminClientAsync(AdminRole.Ops)).GetAsync("/api/v1/admin/audit.csv", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var csv = await (await Factory.AdminClientAsync(AdminRole.Finance)).GetAsync($"/api/v1/admin/audit.csv?entityType=drugs&entityId={created.Id}", Ct);
        csv.StatusCode.ShouldBe(HttpStatusCode.OK);
        csv.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
        var lines = (await csv.Content.ReadAsStringAsync(Ct)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].ShouldBe("id,created_at,actor_type,actor_id,actor_email,action,entity_type,entity_id,changes,ip,request_id");
        lines.Length.ShouldBe(3);
        lines[1].ShouldContain(",admin,");
        lines[1].ShouldContain(",created,drugs,");
    }
}
