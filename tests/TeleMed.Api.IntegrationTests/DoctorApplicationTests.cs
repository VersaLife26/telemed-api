using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Abstractions;
using TeleMed.Application.DoctorApplications;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.DoctorFlows;

namespace TeleMed.Api.IntegrationTests;

public class DoctorApplicationTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Documents_can_only_be_uploaded_with_the_applications_token()
    {
        var created = await Factory.SubmitApplicationAsync();
        created.UploadToken.ShouldNotBeNullOrWhiteSpace();

        var missing = await Factory.UploadApplicationDocumentAsync(created.Id, "slmcCertificate", null, Pdf);
        missing.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await missing.ProblemCodeAsync()).ShouldBe("invalid_upload_token");

        var other = await Factory.SubmitApplicationAsync(new ApplicationSpec { Phone = "+94771000002", Email = "other@example.com", SlmcNumber = "22222" });
        (await Factory.UploadApplicationDocumentAsync(created.Id, "slmcCertificate", other.UploadToken, Pdf))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var document = await (await Factory.UploadApplicationDocumentAsync(created.Id, "slmcCertificate", created.UploadToken, Pdf, "slmc.pdf"))
            .ReadAsync<DoctorDocumentDto>();
        document.Type.ShouldBe(DoctorDocumentType.SlmcCertificate);
        document.ContentType.ShouldBe("application/pdf");
        document.FileName.ShouldBe("slmc.pdf");
        document.SizeBytes.ShouldBe(Pdf.Length);
        document.Sha256.Length.ShouldBe(64);
    }

    [Fact]
    public async Task Uploading_the_same_type_again_replaces_the_live_document()
    {
        var created = await Factory.SubmitApplicationAsync();

        var first = await (await Factory.UploadApplicationDocumentAsync(created.Id, "nic", created.UploadToken, Pdf)).ReadAsync<DoctorDocumentDto>();
        var second = await (await Factory.UploadApplicationDocumentAsync(created.Id, "nic", created.UploadToken, Png)).ReadAsync<DoctorDocumentDto>();

        second.Id.ShouldNotBe(first.Id);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM doctor_documents WHERE application_id = '{created.Id}' AND deleted_at IS NULL")).ShouldBe(1);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM doctor_documents WHERE id = '{first.Id}' AND deleted_at IS NOT NULL")).ShouldBe(1);
    }

    [Theory]
    [InlineData("signature", "pdf")]
    [InlineData("seal", "pdf")]
    [InlineData("slmcCertificate", "text")]
    [InlineData("other", "png")]
    [InlineData("slmc_certificate", "png")]
    public async Task Upload_rejects_disallowed_types_and_contents(string type, string content)
    {
        var created = await Factory.SubmitApplicationAsync();
        var bytes = content switch
        {
            "pdf" => Pdf,
            "png" => Png,
            _ => "just some text"u8.ToArray(),
        };

        (await Factory.UploadApplicationDocumentAsync(created.Id, type, created.UploadToken, bytes)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_rejects_files_over_five_megabytes()
    {
        var created = await Factory.SubmitApplicationAsync();
        var big = new byte[(5 * 1024 * 1024) + 1];
        Pdf.CopyTo(big, 0);

        (await Factory.UploadApplicationDocumentAsync(created.Id, "nic", created.UploadToken, big)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Documents_are_frozen_once_review_starts()
    {
        var created = await Factory.SubmitApplicationAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Ops);
        (await admin.PostAsync($"/api/v1/admin/doctor-applications/{created.Id}/start-review", null, TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await Factory.UploadApplicationDocumentAsync(created.Id, "nic", created.UploadToken, Pdf);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe("application_not_pending");
    }

    [Fact]
    public async Task A_second_open_application_for_the_same_phone_or_slmc_number_conflicts()
    {
        await Factory.SubmitApplicationAsync();

        var samePhone = await Factory.ApplyAsync(new ApplicationSpec { Email = "second@example.com", SlmcNumber = "99999" });
        samePhone.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await samePhone.ProblemCodeAsync()).ShouldBe("application_exists");

        var sameSlmc = await Factory.ApplyAsync(new ApplicationSpec { Phone = "+94771000009", Email = "third@example.com" });
        sameSlmc.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await sameSlmc.ProblemCodeAsync()).ShouldBe("application_exists");
    }

    [Fact]
    public async Task A_rejected_applicant_can_apply_again()
    {
        var created = await Factory.SubmitApplicationAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Admin);
        (await admin.PostJsonAsync($"/api/v1/admin/doctor-applications/{created.Id}/reject", new { reason = "Blurry certificate" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await Factory.ApplyAsync(new ApplicationSpec())).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Applying_with_an_existing_accounts_phone_or_email_conflicts()
    {
        var spec = new ApplicationSpec();
        await Factory.SignInWithPhoneAsync(spec.Phone);
        await Factory.CreateUserAsync(UserRole.Patient, email: "taken@example.com");

        var samePhone = await Factory.ApplyAsync(spec);
        var sameEmail = await Factory.ApplyAsync(spec with { Phone = "+94771000077", Email = "Taken@Example.com" });

        samePhone.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await samePhone.ProblemCodeAsync()).ShouldBe("account_exists");
        sameEmail.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await sameEmail.ProblemCodeAsync()).ShouldBe("account_exists");
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM doctor_applications")).ShouldBe(0);
    }

    [Fact]
    public async Task Applying_with_a_registered_doctors_slmc_number_conflicts()
    {
        await Factory.ApprovedDoctorAsync();

        var response = await Factory.ApplyAsync(new ApplicationSpec { Phone = "+94771000005", Email = "copy@example.com" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe("slmc_registered");
    }

    public static TheoryData<string, ApplicationSpec> InvalidApplications => new()
    {
        { "termsAccepted", new ApplicationSpec { TermsAccepted = false } },
        { "phone", new ApplicationSpec { Phone = "+1 555 0100" } },
        { "slmcNumber", new ApplicationSpec { SlmcNumber = "not-a-number" } },
        { "languages", new ApplicationSpec { Languages = [] } },
        { "languageOther", new ApplicationSpec { Languages = ["en", "other"] } },
        { "feeCents", new ApplicationSpec { FeeCents = 100 } },
        { "feeCents", new ApplicationSpec { FeeCents = 50_000_000 } },
        { "password", new ApplicationSpec { Password = "short" } },
        { "specialtyCode", new ApplicationSpec { SpecialtyCode = "astrology" } },
    };

    [Theory]
    [MemberData(nameof(InvalidApplications))]
    public async Task Invalid_applications_are_rejected(string field, ApplicationSpec spec)
    {
        var response = await Factory.ApplyAsync(spec);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain($"\"{field}\"");
    }

    [Fact]
    public async Task Password_is_optional_and_other_language_needs_its_name()
    {
        var response = await Factory.ApplyAsync(new ApplicationSpec { Password = null, Languages = ["ta", "other"], LanguageOther = "Hindi" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Eligibility_follows_the_application_to_an_active_doctor()
    {
        var client = Factory.CreateClient();
        async Task<EligibilityDto> CheckAsync() =>
            await (await client.GetAsync("/api/v1/doctor-applications/eligibility?phone=0771000001", TestContext.Current.CancellationToken))
                .ReadAsync<EligibilityDto>();

        (await CheckAsync()).Status.ShouldBe(ApplicantEligibility.None);

        var created = await Factory.SubmitApplicationAsync();
        var pending = await CheckAsync();
        pending.Status.ShouldBe(ApplicantEligibility.Pending);
        pending.ApplicationId.ShouldBe(created.Id);

        var approved = await (await Factory.AdminClientAsync(AdminRole.Admin)).ApproveAsync(created.Id);
        var doctor = await CheckAsync();
        doctor.Status.ShouldBe(ApplicantEligibility.Doctor);
        doctor.DoctorId.ShouldBe(approved.DoctorId);

        (await client.GetAsync("/api/v1/doctor-applications/eligibility?phone=abc", TestContext.Current.CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Bank_account_details_are_stored_encrypted()
    {
        const string accountNumber = "7788990011223344";
        var (doctorId, _) = await Factory.ApprovedDoctorAsync(new ApplicationSpec { AccountNumber = accountNumber });

        var applicationCipher = await Fixture.ScalarAsync<string>("SELECT bank_account_encrypted FROM doctor_applications");
        var doctorCipher = await Fixture.ScalarAsync<string>($"SELECT bank_account_encrypted FROM doctors WHERE id = '{doctorId}'");
        var rawRows = await Fixture.ScalarAsync<long>(
            $"SELECT (SELECT count(*) FROM doctor_applications WHERE doctor_applications::text LIKE '%{accountNumber}%') + " +
            $"(SELECT count(*) FROM doctors WHERE doctors::text LIKE '%{accountNumber}%') + " +
            $"(SELECT count(*) FROM audit_logs WHERE changes::text LIKE '%{accountNumber}%')");

        rawRows.ShouldBe(0);
        applicationCipher!.ShouldNotContain(accountNumber);
        doctorCipher.ShouldBe(applicationCipher);
        await using var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IBankDataCipher>().Decrypt(applicationCipher!).ShouldContain(accountNumber);
    }

    [Fact]
    public async Task Application_secrets_are_not_audited()
    {
        await Factory.SubmitApplicationAsync();

        var changes = await Fixture.ScalarAsync<string>("SELECT changes::text FROM audit_logs WHERE entity_type = 'doctor_applications'");

        changes.ShouldNotBeNull();
        changes.ShouldContain("slmc_number");
        changes.ShouldNotContain("password_hash");
        changes.ShouldNotContain("upload_token_hash");
        changes.ShouldNotContain("bank_account_encrypted");
        changes.ShouldNotContain("xmin");
        var languages = System.Text.Json.JsonDocument.Parse(changes).RootElement.GetProperty("languages").GetProperty("new");
        languages.EnumerateArray().Select(l => l.GetString()).ShouldBe(["en", "si"]);
    }
}
