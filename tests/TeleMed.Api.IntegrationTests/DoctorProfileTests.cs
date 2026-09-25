using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.Doctors;
using TeleMed.Application.Common;
using TeleMed.Application.Doctors;
using TeleMed.Application.Users;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.DoctorFlows;

namespace TeleMed.Api.IntegrationTests;

public class DoctorProfileTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object Update(DoctorProfileDto current, long? feeCents = null, uint? version = null) => new
    {
        displayName = "Dr. Nimal Perera",
        bio = "Family medicine and diabetes care.",
        subSpecialties = new[] { "Diabetes" },
        languages = new[] { "en", "ta" },
        qualifications = new[] { new { degree = "MBBS", institution = "University of Colombo", year = 2010 } },
        experienceYears = 12,
        feeCents = feeCents ?? 300_000,
        acceptsNewPatients = false,
        version = version ?? current.Version,
    };

    [Fact]
    public async Task Profile_updates_use_optimistic_concurrency()
    {
        var (_, doctor) = await Factory.ApprovedDoctorAsync();
        var current = await doctor.ProfileAsync();

        var updated = await (await doctor.PutJsonAsync("/api/v1/doctors/me", Update(current))).ReadAsync<DoctorProfileDto>();

        updated.DisplayName.ShouldBe("Dr. Nimal Perera");
        updated.Languages.ShouldBe([ConsultationLanguage.En, ConsultationLanguage.Ta]);
        updated.Qualifications.Single().Year.ShouldBe(2010);
        updated.FeeCents.ShouldBe(300_000);
        updated.AcceptsNewPatients.ShouldBeFalse();
        updated.Version.ShouldNotBe(current.Version);
        (await doctor.ProfileAsync()).ShouldBeEquivalentTo(updated);

        var stale = await doctor.PutJsonAsync("/api/v1/doctors/me", Update(current));
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await stale.ProblemCodeAsync()).ShouldBe("concurrency_conflict");
    }

    [Theory]
    [InlineData(49_999)]
    [InlineData(5_000_001)]
    public async Task Fee_must_stay_within_platform_caps(long feeCents)
    {
        var (_, doctor) = await Factory.ApprovedDoctorAsync();
        var current = await doctor.ProfileAsync();

        (await doctor.PutJsonAsync("/api/v1/doctors/me", Update(current, feeCents))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Suspended_doctor_can_read_but_not_write_and_is_hidden_from_patients()
    {
        var (doctorId, doctor) = await Factory.ApprovedDoctorAsync();
        var current = await doctor.ProfileAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Support);
        var anonymous = Factory.CreateClient();

        (await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/suspend", new { reason = "" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var suspended = await (await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/suspend", new { reason = "Licence under review" }))
            .ReadAsync<AdminDoctorDto>();
        suspended.Status.ShouldBe(DoctorStatus.Suspended);
        suspended.SuspendedReason.ShouldBe("Licence under review");

        (await doctor.ProfileAsync()).Status.ShouldBe(DoctorStatus.Suspended);
        var write = await doctor.PutJsonAsync("/api/v1/doctors/me", Update(current));
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await write.ProblemCodeAsync()).ShouldBe("doctor_suspended");
        (await doctor.PutAsync("/api/v1/doctors/me/photo", File(Png), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await doctor.PutAsync("/api/v1/doctors/me/signature", File(Png), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await (await anonymous.GetAsync("/api/v1/doctors", Ct)).ReadAsync<PagedResult<PublicDoctorDto>>()).Total.ShouldBe(0);
        (await anonymous.GetAsync($"/api/v1/doctors/{doctorId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await admin.PostAsync($"/api/v1/admin/doctors/{doctorId}/reinstate", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await anonymous.GetAsync($"/api/v1/doctors/{doctorId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await doctor.PutJsonAsync("/api/v1/doctors/me", Update(await doctor.ProfileAsync()))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Photo_is_served_through_signed_urls_and_can_be_removed()
    {
        var (doctorId, doctor) = await Factory.ApprovedDoctorAsync();
        var anonymous = Factory.CreateClient();
        (await anonymous.GetAsync($"/api/v1/doctors/{doctorId}/photo", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await doctor.PutAsync("/api/v1/doctors/me/photo", File("not an image"u8.ToArray()), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var profile = await (await doctor.PutAsync("/api/v1/doctors/me/photo", File(Png), Ct)).ReadAsync<DoctorProfileDto>();
        profile.PhotoUrl.ShouldNotBeNull();

        var publicPhoto = await (await anonymous.GetAsync($"/api/v1/doctors/{doctorId}/photo", Ct)).ReadAsync<PhotoUrlDto>();
        (await anonymous.GetByteArrayAsync(publicPhoto.Url, Ct)).ShouldBe(Png);
        (await (await anonymous.GetAsync($"/api/v1/doctors/{doctorId}", Ct)).ReadAsync<PublicDoctorDto>()).PhotoUrl.ShouldNotBeNull();

        (await doctor.DeleteAsync("/api/v1/doctors/me/photo", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await doctor.GetAsync("/api/v1/doctors/me/photo", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymous.GetAsync(publicPhoto.Url, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("seal")]
    public async Task Signature_and_seal_accept_only_images_and_replace_the_previous_one(string kind)
    {
        var (doctorId, doctor) = await Factory.ApprovedDoctorAsync();
        (await doctor.GetAsync($"/api/v1/doctors/me/{kind}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await doctor.PutAsync($"/api/v1/doctors/me/{kind}", File(Pdf), Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var first = await (await doctor.PutAsync($"/api/v1/doctors/me/{kind}", File(Png), Ct)).ReadAsync<DoctorDocumentDto>();
        var second = await (await doctor.PutAsync($"/api/v1/doctors/me/{kind}", File(Png), Ct)).ReadAsync<DoctorDocumentDto>();

        second.Id.ShouldNotBe(first.Id);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM doctor_documents WHERE doctor_id = '{doctorId}' AND type = '{kind}' AND deleted_at IS NULL"))
            .ShouldBe(1);
        var url = await (await doctor.GetAsync($"/api/v1/doctors/me/{kind}", Ct)).ReadAsync<SignedUrlDto>();
        (await Factory.CreateClient().GetByteArrayAsync(url.Url, Ct)).ShouldBe(Png);
    }

    [Fact]
    public async Task Credential_documents_include_the_application_uploads_and_accept_new_ones()
    {
        var spec = new ApplicationSpec();
        var created = await Factory.SubmitApplicationAsync(spec);
        await Factory.UploadApplicationDocumentAsync(created.Id, "slmcCertificate", created.UploadToken, Pdf);
        await (await Factory.AdminClientAsync(AdminRole.Admin)).ApproveAsync(created.Id);
        var login = await (await Factory.CreateClient().PostJsonAsync("/api/v1/auth/login/email", new { email = spec.Email, password = spec.Password }))
            .ReadAsync<TeleMed.Application.Auth.AuthResponse>();
        var doctor = Factory.CreateClient().WithBearer(login.AccessToken);

        var form = File(Pdf, "board.pdf");
        form.Add(new StringContent("specialtyBoardCertificate"), "type");
        var uploaded = await (await doctor.PostAsync("/api/v1/doctors/me/documents", form, Ct)).ReadAsync<DoctorDocumentDto>(HttpStatusCode.Created);
        uploaded.Type.ShouldBe(DoctorDocumentType.SpecialtyBoardCertificate);

        var signature = File(Png);
        signature.Add(new StringContent("signature"), "type");
        (await doctor.PostAsync("/api/v1/doctors/me/documents", signature, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var documents = await (await doctor.GetAsync("/api/v1/doctors/me/documents", Ct)).ReadAsync<List<DoctorDocumentDto>>();
        documents.Select(d => d.Type).ShouldBe([DoctorDocumentType.SlmcCertificate, DoctorDocumentType.SpecialtyBoardCertificate]);
        (await Factory.CreateClient().GetByteArrayAsync(documents[1].DownloadUrl, Ct)).ShouldBe(Pdf);

        var doctorId = (await doctor.ProfileAsync()).Id;
        var admin = await Factory.AdminClientAsync(AdminRole.Ops);
        var url = await (await admin.GetAsync($"/api/v1/admin/doctors/{doctorId}/documents/{uploaded.Id}", Ct)).ReadAsync<SignedUrlDto>();
        (await Factory.CreateClient().GetByteArrayAsync(url.Url, Ct)).ShouldBe(Pdf);
    }

    [Fact]
    public async Task Admins_list_and_inspect_doctors()
    {
        var (doctorId, _) = await Factory.ApprovedDoctorAsync();
        await Factory.ApprovedDoctorAsync(new ApplicationSpec
        {
            Phone = "+94771000002", Email = "kamala@example.com", SlmcNumber = "22222", FirstName = "Kamala", LastName = "Silva",
        });
        var admin = await Factory.AdminClientAsync(AdminRole.Support);
        await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/suspend", new { reason = "Complaint" });

        var suspended = await (await admin.GetAsync("/api/v1/admin/doctors?status=suspended", Ct)).ReadAsync<PagedResult<AdminDoctorListItemDto>>();
        suspended.Items.Single().Id.ShouldBe(doctorId);
        var byName = await (await admin.GetAsync("/api/v1/admin/doctors?q=kamala", Ct)).ReadAsync<PagedResult<AdminDoctorListItemDto>>();
        byName.Items.Single().Email.ShouldBe("kamala@example.com");

        var detail = await (await admin.GetAsync($"/api/v1/admin/doctors/{doctorId}", Ct)).ReadAsync<AdminDoctorDto>();
        detail.Phone.ShouldBe("+94771000001");
        detail.Status.ShouldBe(DoctorStatus.Suspended);
        (await admin.GetAsync($"/api/v1/admin/doctors/{Guid.NewGuid()}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
