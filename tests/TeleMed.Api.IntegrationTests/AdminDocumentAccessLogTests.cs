using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class AdminDocumentAccessLogTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_views_of_doctor_documents_are_written_to_the_record_access_log()
    {
        var application = await Factory.SubmitApplicationAsync();
        var document = await (await Factory.UploadApplicationDocumentAsync(application.Id, "slmcCertificate", application.UploadToken, DoctorFlows.Pdf))
            .ReadAsync<DoctorDocumentDto>();
        var adminUser = await Factory.CreateAdminAsync(AdminRole.Admin);
        var admin = Factory.CreateAdminClient(Factory.LocalAdminToken(adminUser.Email));

        (await admin.GetAsync($"/api/v1/admin/doctor-applications/{application.Id}/documents/{document.Id}", Ct)).EnsureSuccessStatusCode();

        var where = $"resource_type = 'doctor_document' AND action = 'view' AND resource_id = '{document.Id}'";
        (await Fixture.ScalarAsync<string>($"SELECT concat_ws('|', actor_role, actor_id, granted::text, reason, coalesce(owner_id::text, '-')) FROM record_access_logs WHERE {where}"))
            .ShouldBe($"admin|{adminUser.Id}|true|admin_review|-");

        var approved = await admin.ApproveAsync(application.Id);
        var doctorUserId = await Fixture.ScalarAsync<Guid>($"SELECT user_id FROM doctors WHERE id = '{approved.DoctorId}'");
        (await admin.GetAsync($"/api/v1/admin/doctors/{approved.DoctorId}/documents/{document.Id}", Ct)).EnsureSuccessStatusCode();

        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM record_access_logs WHERE {where}")).ShouldBe(2);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM record_access_logs WHERE {where} AND owner_id = '{doctorUserId}'")).ShouldBe(1);
    }
}
