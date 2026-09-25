using System.Net;
using System.Text.Json;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.Credentialing;
using TeleMed.Application.Auth;
using TeleMed.Application.Common;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.DoctorFlows;

namespace TeleMed.Api.IntegrationTests;

public class CredentialingTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Approval_creates_the_doctor_account_and_profile_and_links_documents()
    {
        var spec = new ApplicationSpec();
        var created = await Factory.SubmitApplicationAsync(spec);
        var signature = await (await Factory.UploadApplicationDocumentAsync(created.Id, "signature", created.UploadToken, Png)).ReadAsync<DoctorDocumentDto>();
        var admin = await Factory.AdminClientAsync(AdminRole.Support);

        var approved = await admin.ApproveAsync(created.Id);

        approved.Status.ShouldBe(DoctorApplicationStatus.Approved);
        approved.DoctorId.ShouldNotBeNull();
        (await Fixture.ScalarAsync<string>($"SELECT role FROM users WHERE phone_number = '{spec.Phone}' AND email = '{spec.Email}'")).ShouldBe("doctor");
        (await Fixture.ScalarAsync<Guid>($"SELECT doctor_id FROM doctor_documents WHERE id = '{signature.Id}'")).ShouldBe(approved.DoctorId.Value);

        var login = await Factory.CreateClient().PostJsonAsync("/api/v1/auth/login/email", new { email = spec.Email, password = spec.Password });
        var auth = await login.ReadAsync<AuthResponse>();
        auth.User.Role.ShouldBe(UserRole.Doctor);

        var doctor = Factory.CreateClient().WithBearer(auth.AccessToken);
        var profile = await doctor.ProfileAsync();
        profile.Id.ShouldBe(approved.DoctorId.Value);
        profile.SlmcNumber.ShouldBe(spec.SlmcNumber);
        profile.Status.ShouldBe(DoctorStatus.Active);
        profile.Languages.ShouldBe([ConsultationLanguage.En, ConsultationLanguage.Si]);
        profile.Qualifications.Single().Degree.ShouldBe("MBBS (Colombo)");

        var stamp = await (await doctor.GetAsync("/api/v1/doctors/me/signature", Ct)).ReadAsync<SignedUrlDto>();
        (await Factory.CreateClient().GetByteArrayAsync(stamp.Url, Ct)).ShouldBe(Png);
    }

    [Fact]
    public async Task Approval_is_atomic_when_the_doctor_row_cannot_be_written()
    {
        var spec = new ApplicationSpec();
        var created = await Factory.SubmitApplicationAsync(spec);
        var document = await (await Factory.UploadApplicationDocumentAsync(created.Id, "nic", created.UploadToken, Pdf)).ReadAsync<DoctorDocumentDto>();
        // A doctor holding the same SLMC number appears after the application was accepted, so only the doctors insert fails.
        var squatter = await Factory.CreateUserAsync(UserRole.Doctor);
        await Fixture.ExecuteSqlAsync($$"""
            INSERT INTO doctors (id, user_id, slmc_number, specialty_code, sub_specialties, display_name, bio, languages, qualifications,
                experience_years, fee_cents, currency, accepts_new_patients, bank_name, bank_branch, bank_account_encrypted, status,
                approved_at, approved_by, created_at, updated_at)
            VALUES (gen_random_uuid(), '{{squatter.User.Id}}', '{{spec.SlmcNumber}}', 'general_practice', '{}', 'Squatter', '', '{en}', '[]',
                1, 100000, 'LKR', true, 'b', 'b', 'x', 'active', now(), gen_random_uuid(), now(), now())
            """);

        var response = await (await Factory.AdminClientAsync(AdminRole.Admin)).PostAsync($"/api/v1/admin/doctor-applications/{created.Id}/approve", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe("slmc_registered");
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM users WHERE phone_number = '{spec.Phone}' OR email = '{spec.Email}'")).ShouldBe(0);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM doctors")).ShouldBe(1);
        (await Fixture.ScalarAsync<string>($"SELECT status FROM doctor_applications WHERE id = '{created.Id}'")).ShouldBe("pending");
        (await Fixture.ScalarAsync<object>($"SELECT doctor_id FROM doctor_documents WHERE id = '{document.Id}'")).ShouldBeNull();
    }

    [Fact]
    public async Task Approval_refuses_to_take_over_an_existing_patient_account()
    {
        var spec = new ApplicationSpec();
        var created = await Factory.SubmitApplicationAsync(spec);
        await Factory.SignInWithPhoneAsync(spec.Phone);

        var response = await (await Factory.AdminClientAsync(AdminRole.Admin)).PostAsync($"/api/v1/admin/doctor-applications/{created.Id}/approve", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe("applicant_account_exists");
        (await Fixture.ScalarAsync<string>($"SELECT role FROM users WHERE phone_number = '{spec.Phone}'")).ShouldBe("patient");
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM doctors")).ShouldBe(0);
    }

    [Fact]
    public async Task Rejection_requires_a_reason_and_closes_the_application()
    {
        var created = await Factory.SubmitApplicationAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Admin);

        (await admin.PostJsonAsync($"/api/v1/admin/doctor-applications/{created.Id}/reject", new { reason = "  " }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync($"/api/v1/admin/doctor-applications/{created.Id}/reject", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var rejected = await (await admin.PostJsonAsync($"/api/v1/admin/doctor-applications/{created.Id}/reject", new { reason = "SLMC number not in registry" }))
            .ReadAsync<DoctorApplicationDto>();
        rejected.Status.ShouldBe(DoctorApplicationStatus.Rejected);
        rejected.RejectionReason.ShouldBe("SLMC number not in registry");

        var approve = await admin.PostAsync($"/api/v1/admin/doctor-applications/{created.Id}/approve", null, Ct);
        approve.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await approve.ProblemCodeAsync()).ShouldBe("invalid_application_status");
    }

    [Fact]
    public async Task Checklist_updates_are_partial_and_audited()
    {
        var created = await Factory.SubmitApplicationAsync();
        var adminUser = await Factory.CreateAdminAsync(AdminRole.Ops);
        var admin = Factory.CreateAdminClient(Factory.LocalAdminToken(adminUser.Email));

        await (await admin.PutJsonAsync($"/api/v1/admin/doctor-applications/{created.Id}/checklist", new { slmcFormat = true })).ReadAsync<DoctorApplicationDto>();
        var updated = await (await admin.PutJsonAsync($"/api/v1/admin/doctor-applications/{created.Id}/checklist", new { nicMatch = false }))
            .ReadAsync<DoctorApplicationDto>();

        updated.Checklist.SlmcFormat.ShouldNotBeNull().Ok.ShouldBeTrue();
        updated.Checklist.SlmcFormat.ByAdminId.ShouldBe(adminUser.Id);
        updated.Checklist.NicMatch.ShouldNotBeNull().Ok.ShouldBeFalse();
        updated.Checklist.Experience.ShouldBeNull();
        (await admin.PutJsonAsync($"/api/v1/admin/doctor-applications/{created.Id}/checklist", new { })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var changes = await Fixture.ScalarAsync<string>(
            $"SELECT changes::text FROM audit_logs WHERE entity_type = 'doctor_applications' AND entity_id = '{created.Id}' " +
            $"AND action = 'updated' AND actor_id = '{adminUser.Id}' ORDER BY id DESC LIMIT 1");
        var checklist = JsonDocument.Parse(changes!).RootElement;
        checklist.EnumerateObject().Select(p => p.Name).ShouldBe(["checklist"]);
        checklist.GetProperty("checklist").GetProperty("old").GetProperty("nicMatch").ValueKind.ShouldBe(JsonValueKind.Null);
        checklist.GetProperty("checklist").GetProperty("new").GetProperty("nicMatch").GetProperty("ok").GetBoolean().ShouldBeFalse();
        checklist.GetProperty("checklist").GetProperty("new").GetProperty("slmcFormat").GetProperty("ok").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Admins_list_applications_by_status_and_open_documents_through_signed_urls()
    {
        var first = await Factory.SubmitApplicationAsync();
        await Factory.SubmitApplicationAsync(new ApplicationSpec { Phone = "+94771000002", Email = "b@example.com", SlmcNumber = "22222" });
        var document = await (await Factory.UploadApplicationDocumentAsync(first.Id, "degreeCertificate", first.UploadToken, Pdf)).ReadAsync<DoctorDocumentDto>();
        var admin = await Factory.AdminClientAsync(AdminRole.Finance);
        (await admin.PostAsync($"/api/v1/admin/doctor-applications/{first.Id}/start-review", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var underReview = await (await admin.GetAsync("/api/v1/admin/doctor-applications?status=underReview", Ct))
            .ReadAsync<PagedResult<DoctorApplicationSummaryDto>>();
        underReview.Total.ShouldBe(1);
        underReview.Items.Single().Id.ShouldBe(first.Id);
        (await (await admin.GetAsync("/api/v1/admin/doctor-applications", Ct)).ReadAsync<PagedResult<DoctorApplicationSummaryDto>>()).Total.ShouldBe(2);

        var detail = await (await admin.GetAsync($"/api/v1/admin/doctor-applications/{first.Id}", Ct)).ReadAsync<DoctorApplicationDto>();
        detail.HasPassword.ShouldBeTrue();
        detail.Documents.Single().Id.ShouldBe(document.Id);

        var url = await (await admin.GetAsync($"/api/v1/admin/doctor-applications/{first.Id}/documents/{document.Id}", Ct)).ReadAsync<SignedUrlDto>();
        (await Factory.CreateClient().GetByteArrayAsync(url.Url, Ct)).ShouldBe(Pdf);
        (await admin.GetAsync($"/api/v1/admin/doctor-applications/{Guid.NewGuid()}/documents/{document.Id}", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patient_and_doctor_tokens_cannot_use_admin_routes()
    {
        var created = await Factory.SubmitApplicationAsync(new ApplicationSpec { Phone = "+94771000003", Email = "c@example.com", SlmcNumber = "33333" });
        var (doctorId, doctor) = await Factory.ApprovedDoctorAsync();
        var patient = Factory.CreateClient().WithBearer((await Factory.SignInWithPhoneAsync("+94779999999")).AccessToken);
        patient.DefaultRequestHeaders.Add("Origin", TeleMedApiFactory.AdminOrigin);
        doctor.DefaultRequestHeaders.Add("Origin", TeleMedApiFactory.AdminOrigin);

        foreach (var client in new[] { patient, doctor })
        {
            (await client.GetAsync("/api/v1/admin/doctor-applications", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await client.PostAsync($"/api/v1/admin/doctor-applications/{created.Id}/approve", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await client.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/suspend", new { reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await Fixture.ScalarAsync<string>($"SELECT status FROM doctor_applications WHERE id = '{created.Id}'")).ShouldBe("pending");
        (await patient.GetAsync("/api/v1/doctors/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
