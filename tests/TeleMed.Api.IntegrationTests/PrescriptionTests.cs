using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Common;
using TeleMed.Application.Prescriptions;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;
using static TeleMed.Api.IntegrationTests.Infrastructure.ClinicalFlows;

namespace TeleMed.Api.IntegrationTests;

public class PrescriptionTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_issued_prescription_renders_as_a_pdf_and_verifies_until_it_is_tampered_with_or_cancelled()
    {
        var (doctor, patient, appointmentId) = await Factory.StartedPaidVisitAsync();
        await doctor.Client.UploadStampsAsync();

        var issued = await (await doctor.Client.PostJsonAsync($"/api/v1/appointments/{appointmentId}/prescription", PrescriptionBody()))
            .ReadAsync<PrescriptionDto>(HttpStatusCode.Created);
        issued.IsTest.ShouldBeFalse();
        issued.DoctorName.ShouldBe("Nimal Perera");
        issued.DoctorSlmc.ShouldBe("12345");
        issued.Items.Select(i => (i.DrugName, i.SortOrder)).ShouldBe([("Amoxicillin", 0), ("Paracetamol", 1)]);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM notifications WHERE template_key = 'prescription_ready' AND user_id = '{patient.UserId}'"))
            .ShouldBeGreaterThan(0);
        (await Fixture.ScalarAsync<long>(
                $"SELECT octet_length(attachment_content) FROM notifications WHERE template_key = 'prescription_ready' AND user_id = '{patient.UserId}' AND channel = 'email'"))
            .ShouldBeGreaterThan(100);

        (await (await patient.Client.GetAsync($"/api/v1/appointments/{appointmentId}/prescription", Ct)).ReadAsync<PrescriptionDto>()).Id.ShouldBe(issued.Id);
        (await (await patient.Client.GetAsync("/api/v1/prescriptions", Ct)).ReadAsync<PagedResult<PrescriptionDto>>()).Items.ShouldHaveSingleItem();
        (await (await doctor.Client.GetAsync("/api/v1/prescriptions", Ct)).ReadAsync<PagedResult<PrescriptionDto>>()).Total.ShouldBe(1);

        var pdf = await patient.Client.GetAsync($"/api/v1/prescriptions/{issued.Id}/pdf", Ct);
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync(Ct);
        bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8).ShouldBeTrue();

        var hmac = (await Fixture.ScalarAsync<string>($"SELECT verification_hmac FROM prescriptions WHERE id = '{issued.Id}'"))!;
        var anonymous = Factory.CreateClient();
        var valid = await VerifyAsync(anonymous, issued.Id, hmac);
        valid.Valid.ShouldBeTrue();
        valid.Reason.ShouldBeNull();
        valid.DoctorSlmc.ShouldBe("12345");
        valid.PatientInitials.ShouldBe("K.S.");
        valid.IssuedAt.ShouldBe(issued.IssuedAt);
        valid.Items!.Select(i => i.Quantity).ShouldBe([21, 10]);

        var flipped = (hmac[0] == '0' ? "1" : "0") + hmac[1..];
        (await VerifyAsync(anonymous, issued.Id, flipped)).ShouldBe(PrescriptionVerificationDto.Invalid);
        (await VerifyAsync(anonymous, issued.Id, "nonsense")).ShouldBe(PrescriptionVerificationDto.Invalid);
        (await VerifyAsync(anonymous, Guid.NewGuid(), hmac)).ShouldBe(PrescriptionVerificationDto.Invalid);

        await Fixture.ExecuteSqlAsync($"UPDATE prescription_items SET quantity = 100 WHERE prescription_id = '{issued.Id}' AND sort_order = 1");
        (await VerifyAsync(anonymous, issued.Id, hmac)).ShouldBe(PrescriptionVerificationDto.Invalid);
        await Fixture.ExecuteSqlAsync($"UPDATE prescription_items SET quantity = 10 WHERE prescription_id = '{issued.Id}' AND sort_order = 1");

        var cancelled = await (await doctor.Client.PostJsonAsync($"/api/v1/prescriptions/{issued.Id}/cancel", new { reason = "Wrong dose" })).ReadAsync<PrescriptionDto>();
        cancelled.Status.ShouldBe(PrescriptionStatus.Cancelled);
        var afterCancel = await VerifyAsync(anonymous, issued.Id, hmac);
        afterCancel.Valid.ShouldBeFalse();
        afterCancel.Reason.ShouldBe(PrescriptionVerificationDto.CancelledReason);

        (await Fixture.AccessLogCountAsync($"resource_id = '{issued.Id}' AND action = 'download' AND granted AND actor_id = '{patient.UserId}'")).ShouldBe(1);
        (await Fixture.AccessLogCountAsync($"resource_id = '{issued.Id}' AND action = 'verify' AND actor_role = 'anonymous'")).ShouldBe(5);
        (await Fixture.AccessLogCountAsync($"resource_id = '{issued.Id}' AND action = 'verify' AND granted")).ShouldBe(1);
    }

    [Fact]
    public async Task A_test_meeting_prescription_is_watermarked_and_never_verifies()
    {
        var (doctor, patient, appointmentId) = await Factory.StartedInstantMeetingAsync();
        await doctor.Client.UploadStampsAsync();

        var issued = await (await doctor.Client.PostJsonAsync($"/api/v1/appointments/{appointmentId}/prescription", PrescriptionBody()))
            .ReadAsync<PrescriptionDto>(HttpStatusCode.Created);

        issued.IsTest.ShouldBeTrue();
        var pdf = await patient.Client.GetAsync($"/api/v1/prescriptions/{issued.Id}/pdf", Ct);
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pdf.Content.ReadAsByteArrayAsync(Ct)).AsSpan(0, 5).SequenceEqual("%PDF-"u8).ShouldBeTrue();
        var hmac = (await Fixture.ScalarAsync<string>($"SELECT verification_hmac FROM prescriptions WHERE id = '{issued.Id}'"))!;
        var result = await VerifyAsync(Factory.CreateClient(), issued.Id, hmac);
        result.Valid.ShouldBeFalse();
        result.Reason.ShouldBe(PrescriptionVerificationDto.TestReason);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM notifications WHERE template_key = 'prescription_ready'")).ShouldBe(0);
    }

    [Fact]
    public async Task Issuing_needs_the_treating_doctor_a_started_consultation_stamps_and_only_one_per_appointment()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var id = await patient.Client.InstantMeetingAsync(new { counterpartUserId = doctor.UserId });
        var url = $"/api/v1/appointments/{id}/prescription";

        (await (await doctor.Client.PostJsonAsync(url, PrescriptionBody())).ProblemCodeAsync()).ShouldBe("consultation_not_started");
        await patient.Client.JoinedAsync(id);
        await doctor.Client.JoinedAsync(id);
        await doctor.Client.AdmittedAsync(id);
        (await (await doctor.Client.PostJsonAsync(url, PrescriptionBody())).ProblemCodeAsync()).ShouldBe("stamps_required");
        await doctor.Client.UploadStampsAsync();
        (await doctor.Client.PostJsonAsync(url, new { items = Array.Empty<object>() })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await doctor.Client.PostJsonAsync(url, new { items = new[] { new { drugName = "X", dosage = "1", frequency = "1x", durationDays = 0, quantity = 1 } } }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await doctor.Client.PostJsonAsync(url, new { items = new[] { new { drugId = Guid.NewGuid(), drugName = "X", dosage = "1", frequency = "1x", durationDays = 1, quantity = 1 } } }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var other = await Factory.BookableDoctorAsync(SecondDoctor);
        await other.Client.UploadStampsAsync();
        (await other.Client.PostJsonAsync(url, PrescriptionBody())).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await patient.Client.PostJsonAsync(url, PrescriptionBody())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var issued = await (await doctor.Client.PostJsonAsync(url, PrescriptionBody())).ReadAsync<PrescriptionDto>(HttpStatusCode.Created);
        (await (await doctor.Client.PostJsonAsync(url, PrescriptionBody())).ProblemCodeAsync()).ShouldBe("prescription_exists");

        var stranger = await Factory.PatientAsync();
        (await stranger.Client.GetAsync($"/api/v1/prescriptions/{issued.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await other.Client.GetAsync($"/api/v1/prescriptions/{issued.Id}/pdf", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await other.Client.PostAsync($"/api/v1/prescriptions/{issued.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Fixture.AccessLogCountAsync($"resource_id = '{issued.Id}' AND granted = false")).ShouldBe(2);
    }

    [Fact]
    public async Task The_right_to_prescribe_lapses_with_the_treating_window()
    {
        var (doctor, patient, id) = await Factory.StartedInstantMeetingAsync();
        await doctor.Client.UploadStampsAsync();
        await doctor.Client.EndedAsync(id);

        Factory.Time.Advance(TimeSpan.FromDays(30) + TimeSpan.FromMinutes(5));
        var client = await Factory.ClientForAsync(doctor.UserId);

        (await (await client.PostJsonAsync($"/api/v1/appointments/{id}/prescription", PrescriptionBody())).ProblemCodeAsync()).ShouldBe("treating_window_closed");
    }

    private static async Task<PrescriptionVerificationDto> VerifyAsync(HttpClient client, Guid id, string hmac) =>
        await (await client.GetAsync($"/api/v1/prescriptions/{id}/verify?h={Uri.EscapeDataString(hmac)}", Ct)).ReadAsync<PrescriptionVerificationDto>();
}
