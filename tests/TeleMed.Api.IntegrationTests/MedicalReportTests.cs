using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Common;
using TeleMed.Application.MedicalReports;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;
using static TeleMed.Api.IntegrationTests.Infrastructure.ClinicalFlows;

namespace TeleMed.Api.IntegrationTests;

public class MedicalReportTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_issued_medical_report_renders_as_a_pdf_and_verifies_until_it_is_tampered_with_or_cancelled()
    {
        var (doctor, patient, appointmentId) = await Factory.StartedPaidVisitAsync();
        await doctor.Client.UploadStampsAsync();

        var issued = await (await doctor.Client.PostJsonAsync($"/api/v1/appointments/{appointmentId}/medical-report", MedicalReportBody()))
            .ReadAsync<MedicalReportDto>(HttpStatusCode.Created);
        issued.IsTest.ShouldBeFalse();
        issued.DoctorName.ShouldBe("Nimal Perera");
        issued.ClinicalImpression.ShouldBe("Acute viral illness");
        issued.Fitness.ShouldBe(FitnessForWork.Unfit);
        issued.LeaveFrom.ShouldBe(new DateOnly(2026, 10, 3));
        issued.ReturnToWorkOn.ShouldBe(new DateOnly(2026, 10, 5));
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM notifications WHERE template_key = 'medical_report_ready' AND user_id = '{patient.UserId}'"))
            .ShouldBeGreaterThan(0);
        (await Fixture.ScalarAsync<int>(
                $"SELECT octet_length(attachment_content) FROM notifications WHERE template_key = 'medical_report_ready' AND user_id = '{patient.UserId}' AND channel = 'email'"))
            .ShouldBeGreaterThan(100);

        (await (await patient.Client.GetAsync($"/api/v1/appointments/{appointmentId}/medical-report", Ct)).ReadAsync<MedicalReportDto>()).Id.ShouldBe(issued.Id);
        (await (await patient.Client.GetAsync("/api/v1/medical-reports", Ct)).ReadAsync<PagedResult<MedicalReportDto>>()).Items.ShouldHaveSingleItem();
        (await (await doctor.Client.GetAsync("/api/v1/medical-reports", Ct)).ReadAsync<PagedResult<MedicalReportDto>>()).Total.ShouldBe(1);

        var pdf = await patient.Client.GetAsync($"/api/v1/medical-reports/{issued.Id}/pdf", Ct);
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync(Ct);
        bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8).ShouldBeTrue();

        var hmac = (await Fixture.ScalarAsync<string>($"SELECT verification_hmac FROM medical_reports WHERE id = '{issued.Id}'"))!;
        var anonymous = Factory.CreateClient();
        var valid = await VerifyAsync(anonymous, issued.Id, hmac);
        valid.Valid.ShouldBeTrue();
        valid.Reason.ShouldBeNull();
        valid.DoctorSlmc.ShouldBe("12345");
        valid.PatientInitials.ShouldBe("K.S.");
        valid.ClinicalImpression.ShouldBe("Acute viral illness");

        var flipped = (hmac[0] == '0' ? "1" : "0") + hmac[1..];
        (await VerifyAsync(anonymous, issued.Id, flipped)).ShouldBe(MedicalReportVerificationDto.Invalid);

        await Fixture.ExecuteSqlAsync($"UPDATE medical_reports SET clinical_impression = 'Tampered' WHERE id = '{issued.Id}'");
        (await VerifyAsync(anonymous, issued.Id, hmac)).ShouldBe(MedicalReportVerificationDto.Invalid);
        await Fixture.ExecuteSqlAsync($"UPDATE medical_reports SET clinical_impression = 'Acute viral illness' WHERE id = '{issued.Id}'");

        var cancelled = await (await doctor.Client.PostJsonAsync($"/api/v1/medical-reports/{issued.Id}/cancel", new { reason = "Wrong dates" }))
            .ReadAsync<MedicalReportDto>();
        cancelled.Status.ShouldBe(MedicalReportStatus.Cancelled);
        var afterCancel = await VerifyAsync(anonymous, issued.Id, hmac);
        afterCancel.Valid.ShouldBeFalse();
        afterCancel.Reason.ShouldBe(MedicalReportVerificationDto.CancelledReason);

        (await Fixture.AccessLogCountAsync($"resource_id = '{issued.Id}' AND action = 'download' AND granted AND actor_id = '{patient.UserId}'")).ShouldBe(1);
    }

    [Fact]
    public async Task A_test_meeting_medical_report_is_watermarked_and_never_notifies()
    {
        var (doctor, patient, appointmentId) = await Factory.StartedInstantMeetingAsync();
        await doctor.Client.UploadStampsAsync();

        var issued = await (await doctor.Client.PostJsonAsync($"/api/v1/appointments/{appointmentId}/medical-report", MedicalReportBody()))
            .ReadAsync<MedicalReportDto>(HttpStatusCode.Created);

        issued.IsTest.ShouldBeTrue();
        var pdf = await patient.Client.GetAsync($"/api/v1/medical-reports/{issued.Id}/pdf", Ct);
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pdf.Content.ReadAsByteArrayAsync(Ct)).AsSpan(0, 5).SequenceEqual("%PDF-"u8).ShouldBeTrue();
        var hmac = (await Fixture.ScalarAsync<string>($"SELECT verification_hmac FROM medical_reports WHERE id = '{issued.Id}'"))!;
        var result = await VerifyAsync(Factory.CreateClient(), issued.Id, hmac);
        result.Valid.ShouldBeFalse();
        result.Reason.ShouldBe(MedicalReportVerificationDto.TestReason);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM notifications WHERE template_key = 'medical_report_ready'")).ShouldBe(0);
    }

    [Fact]
    public async Task Issuing_needs_the_treating_doctor_a_started_consultation_stamps_and_only_one_per_appointment()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var id = await patient.Client.InstantMeetingAsync(new { counterpartUserId = doctor.UserId });
        var url = $"/api/v1/appointments/{id}/medical-report";

        (await (await doctor.Client.PostJsonAsync(url, MedicalReportBody())).ProblemCodeAsync()).ShouldBe("consultation_not_started");
        await patient.Client.JoinedAsync(id);
        await doctor.Client.JoinedAsync(id);
        await doctor.Client.AdmittedAsync(id);
        (await (await doctor.Client.PostJsonAsync(url, MedicalReportBody())).ProblemCodeAsync()).ShouldBe("stamps_required");
        await doctor.Client.UploadStampsAsync();
        (await doctor.Client.PostJsonAsync(url, new { clinicalImpression = "" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await doctor.Client.PostJsonAsync(url, new { clinicalImpression = "Viral illness", fitness = "unfit" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var other = await Factory.BookableDoctorAsync(SecondDoctor);
        await other.Client.UploadStampsAsync();
        (await other.Client.PostJsonAsync(url, MedicalReportBody())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await patient.Client.PostJsonAsync(url, MedicalReportBody())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var issued = await (await doctor.Client.PostJsonAsync(url, MedicalReportBody())).ReadAsync<MedicalReportDto>(HttpStatusCode.Created);
        (await (await doctor.Client.PostJsonAsync(url, MedicalReportBody())).ProblemCodeAsync()).ShouldBe("medical_report_exists");

        var stranger = await Factory.PatientAsync();
        (await stranger.Client.GetAsync($"/api/v1/medical-reports/{issued.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await other.Client.GetAsync($"/api/v1/medical-reports/{issued.Id}/pdf", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await other.Client.PostAsync($"/api/v1/medical-reports/{issued.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Fixture.AccessLogCountAsync($"resource_id = '{issued.Id}' AND granted = false")).ShouldBe(2);
    }

    private static async Task<MedicalReportVerificationDto> VerifyAsync(HttpClient client, Guid id, string hmac) =>
        await (await client.GetAsync($"/api/v1/medical-reports/{id}/verify?h={Uri.EscapeDataString(hmac)}", Ct)).ReadAsync<MedicalReportVerificationDto>();
}
