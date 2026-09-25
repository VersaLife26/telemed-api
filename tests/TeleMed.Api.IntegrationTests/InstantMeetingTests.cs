using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Consultations;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.ConsultationFlows;

namespace TeleMed.Api.IntegrationTests;

public class InstantMeetingTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task An_instant_meeting_runs_end_to_end_without_money()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();

        var id = await patient.Client.InstantMeetingAsync(new { counterpartEmail = doctor.Spec.Email.ToUpperInvariant() });

        var appointment = await patient.Client.AppointmentAsync(id);
        appointment.IsTest.ShouldBeTrue();
        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
        appointment.FeeCents.ShouldBe(0);
        appointment.StartAt.ShouldBe(Factory.Time.GetUtcNow());
        (appointment.EndAt - appointment.StartAt).ShouldBe(TimeSpan.FromMinutes(30));
        (await patient.Client.ConsultationAsync(id)).Id.ShouldNotBeNull();

        var patientJoin = await patient.Client.JoinedAsync(id);
        patientJoin.Status.ShouldBe(ConsultationStatus.Waiting);
        var doctorJoin = await doctor.Client.JoinedAsync(id);
        await using var patientPeer = await Factory.ConnectAsync(patientJoin.RoomToken);
        await patientPeer.NextAsync("welcome");
        await using var doctorPeer = await Factory.ConnectAsync(doctorJoin.RoomToken);
        await patientPeer.NextAsync("peer-joined");
        (await doctor.Client.AdmittedAsync(id)).Status.ShouldBe(ConsultationStatus.Active);
        await doctorPeer.SendAsync(new { type = "offer", data = new { sdp = "v=0" } });
        await patientPeer.NextAsync("offer");

        (await doctor.Client.EndedAsync(id)).Status.ShouldBe(ConsultationStatus.Ended);
        await patientPeer.NextAsync("room-closed");
        await Factory.SettleAsync();

        (await doctor.Client.AppointmentAsync(id)).Status.ShouldBe(AppointmentStatus.Completed);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM payments WHERE appointment_id = '{id}'")).ShouldBe(0);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM notifications WHERE user_id = '{patient.UserId}'")).ShouldBe(0);
    }

    [Fact]
    public async Task Doctors_can_start_one_with_a_patient_found_by_phone_and_ids_must_name_the_opposite_role()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var byPhone = await Factory.SignInWithPhoneAsync("+94770000123");

        var id = await doctor.Client.InstantMeetingAsync(new { counterpartPhone = "077 000 0123" });

        (await doctor.Client.AppointmentAsync(id)).PatientId.ShouldBe(byPhone.User.Id);
        var otherPatient = await Factory.PatientAsync();
        var sameRole = await otherPatient.Client.CreateInstantMeetingAsync(new { counterpartUserId = byPhone.User.Id });
        sameRole.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await sameRole.ProblemCodeAsync()).ShouldBe("same_role");
        (await otherPatient.Client.CreateInstantMeetingAsync(new { counterpartUserId = Guid.NewGuid() })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await otherPatient.Client.CreateInstantMeetingAsync(new { counterpartUserId = doctor.UserId, counterpartEmail = doctor.Spec.Email }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await otherPatient.Client.CreateInstantMeetingAsync(new { })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_endpoint_needs_its_switch_the_shared_secret_and_a_signed_in_caller()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var body = new { counterpartUserId = doctor.UserId };

        (await patient.Client.PostJsonAsync("/api/v1/test/instant-meetings", body)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Factory.CreateClient().CreateInstantMeetingAsync(body)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await using var switchedOff = Factory.WithSettings(("Testing:InstantMeetings:Enabled", "false"));
        var client = switchedOff.CreateClient().WithBearer(patient.Client.DefaultRequestHeaders.Authorization!.Parameter!);
        (await client.CreateInstantMeetingAsync(body)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await switchedOff.CreateClient().CreateInstantMeetingAsync(body)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Instant_meetings_show_up_in_both_participants_appointment_lists()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var id = await doctor.Client.InstantMeetingAsync(new { counterpartUserId = patient.UserId });

        foreach (var client in new[] { patient.Client, doctor.Client })
        {
            var listed = await (await client.GetAsync("/api/v1/appointments", TestContext.Current.CancellationToken)).ReadAsync<PagedResult<AppointmentDto>>();
            var meeting = listed.Items.ShouldHaveSingleItem();
            meeting.Id.ShouldBe(id);
            meeting.IsTest.ShouldBeTrue();
        }

        var stranger = await Factory.PatientAsync();
        (await (await stranger.Client.GetAsync("/api/v1/appointments", TestContext.Current.CancellationToken)).ReadAsync<PagedResult<AppointmentDto>>())
            .Total.ShouldBe(0);
    }
}
