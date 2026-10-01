using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Consultations;
using TeleMed.Application.Jobs;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;
using static TeleMed.Api.IntegrationTests.Infrastructure.ConsultationFlows;
using static TeleMed.Api.IntegrationTests.Infrastructure.FinanceFlows;

namespace TeleMed.Api.IntegrationTests;

public class ConsultationTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Join_windows_open_15_minutes_early_and_close_at_the_end_or_two_hours_later_for_the_doctor()
    {
        var (doctor, patient, booked) = await PaidAppointmentAsync();

        AdvanceTo(booked.StartAt - PlatformPolicy.JoinOpensBefore - TimeSpan.FromSeconds(1));
        var tooEarly = await (await Factory.ClientForAsync(patient.UserId)).JoinAsync(booked.Id);
        tooEarly.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await tooEarly.ProblemCodeAsync()).ShouldBe("too_early");
        (await (await Factory.ClientForAsync(doctor.UserId)).JoinAsync(booked.Id)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        Factory.Time.Advance(TimeSpan.FromSeconds(1));
        var patientJoin = await (await Factory.ClientForAsync(patient.UserId)).JoinedAsync(booked.Id);
        patientJoin.Role.ShouldBe(UserRole.Patient);
        patientJoin.Status.ShouldBe(ConsultationStatus.Waiting);
        patientJoin.HubUrl.ShouldBe("/hubs/consultation");
        patientJoin.RoomToken.ShouldNotBeNullOrWhiteSpace();
        patientJoin.IceServers.Single().Urls.ShouldBe(["stun:stun.cloudflare.com:3478"]);
        patientJoin.CounterpartName.ShouldNotBeNullOrWhiteSpace();
        patientJoin.ScheduledStartAt.ShouldBe(booked.StartAt);
        patientJoin.ScheduledEndAt.ShouldBe(booked.EndAt);
        var doctorJoin = await (await Factory.ClientForAsync(doctor.UserId)).JoinedAsync(booked.Id);
        doctorJoin.CounterpartName.ShouldBe("Kamal Silva");
        doctorJoin.ConsultationId.ShouldBe(patientJoin.ConsultationId);

        AdvanceTo(booked.EndAt);
        var closed = await (await Factory.ClientForAsync(patient.UserId)).JoinAsync(booked.Id);
        (await closed.ProblemCodeAsync()).ShouldBe("join_window_closed");
        (await (await Factory.ClientForAsync(doctor.UserId)).JoinAsync(booked.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        AdvanceTo(booked.EndAt + PlatformPolicy.DoctorJoinGraceAfterEnd - TimeSpan.FromSeconds(1));
        (await (await Factory.ClientForAsync(doctor.UserId)).JoinAsync(booked.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.Time.Advance(TimeSpan.FromSeconds(1));
        (await (await (await Factory.ClientForAsync(doctor.UserId)).JoinAsync(booked.Id)).ProblemCodeAsync()).ShouldBe("join_window_closed");
    }

    [Fact]
    public async Task Only_participants_see_the_consultation_and_only_confirmed_appointments_open()
    {
        var (_, patient, booked) = await PaidAppointmentAsync();
        AdvanceTo(booked.StartAt);
        var stranger = await Factory.PatientAsync();
        var otherDoctor = await Factory.BookableDoctorAsync(SecondDoctor);

        (await stranger.Client.JoinAsync(booked.Id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.Client.GetAsync(Url(booked.Id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.Client.GetAsync(Url(booked.Id, "/messages"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await otherDoctor.Client.JoinAsync(booked.Id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await otherDoctor.Client.PostAsync(Url(booked.Id, "/admit"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var patientClient = await Factory.ClientForAsync(patient.UserId);
        (await patientClient.ConsultationAsync(booked.Id)).ShouldSatisfyAllConditions(
            c => c.Id.ShouldBeNull(),
            c => c.Status.ShouldBe(ConsultationStatus.Scheduled));

        var unpaid = await patientClient.BookedAsync(otherDoctor.DoctorId, await Factory.FreeSlotAsync(otherDoctor.DoctorId, TimeSpan.FromMinutes(5)));
        (await (await patientClient.JoinAsync(unpaid.Id)).ProblemCodeAsync()).ShouldBe("not_confirmed");
    }

    [Fact]
    public async Task Waiting_room_admit_and_doctor_end_complete_the_appointment_and_release_the_payment_for_capture()
    {
        var (doctor, patient, booked) = await PaidAppointmentAsync();
        AdvanceTo(booked.StartAt);
        var patientClient = await Factory.ClientForAsync(patient.UserId);
        var doctorClient = await Factory.ClientForAsync(doctor.UserId);

        await patientClient.JoinedAsync(booked.Id);
        (await (await patientClient.GetAsync(Url(booked.Id, "/waiting-room"), Ct)).ReadAsync<WaitingRoomDto>())
            .ShouldBe(new WaitingRoomDto(true, 1, 0, 0));
        (await patientClient.PostAsync(Url(booked.Id, "/admit"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await doctorClient.JoinedAsync(booked.Id);
        var admitted = await doctorClient.AdmittedAsync(booked.Id);
        admitted.Status.ShouldBe(ConsultationStatus.Active);
        admitted.AdmittedAt.ShouldNotBeNull();
        admitted.StartedAt.ShouldNotBeNull();
        (await (await doctorClient.PostAsync(Url(booked.Id, "/admit"), null, Ct)).ProblemCodeAsync()).ShouldBe("not_waiting");
        (await (await patientClient.GetAsync(Url(booked.Id, "/waiting-room"), Ct)).ReadAsync<WaitingRoomDto>()).Waiting.ShouldBeFalse();

        Factory.Time.Advance(TimeSpan.FromMinutes(10));
        doctorClient = await Factory.ClientForAsync(doctor.UserId);
        var ended = await doctorClient.EndedAsync(booked.Id, "Consultation complete");
        ended.Status.ShouldBe(ConsultationStatus.Ended);
        ended.EndedByRole.ShouldBe(UserRole.Doctor);
        ended.EndReason.ShouldBe("Consultation complete");
        ended.DurationSeconds.ShouldBe(600);
        (await doctorClient.AppointmentAsync(booked.Id)).Status.ShouldBe(AppointmentStatus.Completed);
        (await PaymentStatusAsync(booked.Id)).ShouldBe("authorized");

        await Factory.SettleAsync();

        (await PaymentStatusAsync(booked.Id)).ShouldBe("succeeded");
        var rejoin = await (await Factory.ClientForAsync(patient.UserId)).JoinAsync(booked.Id);
        (await rejoin.ProblemCodeAsync()).ShouldBe("not_confirmed");
    }

    [Fact]
    public async Task A_patient_ending_the_call_leaves_the_appointment_to_the_doctor()
    {
        var (doctor, patient, booked) = await PaidAppointmentAsync();
        AdvanceTo(booked.StartAt);
        var patientClient = await Factory.ClientForAsync(patient.UserId);
        await patientClient.JoinedAsync(booked.Id);

        var ended = await (await patientClient.PostAsync(Url(booked.Id, "/end"), null, Ct)).ReadAsync<ConsultationDto>();

        ended.EndedByRole.ShouldBe(UserRole.Patient);
        ended.EndReason.ShouldBe(PlatformPolicy.ConsultationEndedReason);
        ended.DurationSeconds.ShouldBeNull();
        (await (await Factory.ClientForAsync(doctor.UserId)).AppointmentAsync(booked.Id)).Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task Three_poor_quality_reports_in_a_row_suggest_audio_only()
    {
        var (_, patient, booked) = await PaidAppointmentAsync();
        AdvanceTo(booked.StartAt);
        var client = await Factory.ClientForAsync(patient.UserId);
        (await (await client.PostJsonAsync(Url(booked.Id, "/quality"), new { quality = "poor" })).ProblemCodeAsync()).ShouldBe("not_joined");
        await client.JoinedAsync(booked.Id);

        async Task<QualityFeedbackDto> ReportAsync(string quality) =>
            await (await client.PostJsonAsync(Url(booked.Id, "/quality"), new { quality, packetLossPct = 12.5, bitrateKbps = 90 })).ReadAsync<QualityFeedbackDto>();

        (await ReportAsync("poor")).Downgrade.ShouldBeFalse();
        (await ReportAsync("lost")).Downgrade.ShouldBeFalse();
        (await ReportAsync("poor")).ShouldBe(new QualityFeedbackDto(true, "audio_only"));
        (await ReportAsync("good")).Downgrade.ShouldBeFalse();
        (await client.PostJsonAsync(Url(booked.Id, "/quality"), new { quality = "awful" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM consultation_events WHERE kind = 'quality' AND actor_role = 'patient'")).ShouldBe(4);
        (await Fixture.ScalarAsync<string>("SELECT data->>'quality' FROM consultation_events WHERE kind = 'quality' ORDER BY id LIMIT 1")).ShouldBe("poor");
    }

    [Fact]
    public async Task Chat_is_persisted_and_validated()
    {
        var (doctor, patient, booked) = await PaidAppointmentAsync();
        AdvanceTo(booked.StartAt);
        var patientClient = await Factory.ClientForAsync(patient.UserId);
        (await (await patientClient.PostJsonAsync(Url(booked.Id, "/messages"), new { body = "hello" })).ProblemCodeAsync()).ShouldBe("not_joined");
        await patientClient.JoinedAsync(booked.Id);

        (await patientClient.PostJsonAsync(Url(booked.Id, "/messages"), new { body = "  " })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await patientClient.PostJsonAsync(Url(booked.Id, "/messages"), new { body = new string('x', 4001) })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var posted = await (await patientClient.PostJsonAsync(Url(booked.Id, "/messages"), new { body = " I have a fever " }))
            .ReadAsync<ConsultationMessageDto>(HttpStatusCode.Created);
        await (await Factory.ClientForAsync(doctor.UserId)).PostJsonAsync(Url(booked.Id, "/messages"), new { body = "How long?" });

        posted.Body.ShouldBe("I have a fever");
        posted.SenderRole.ShouldBe(UserRole.Patient);
        var page = await (await patientClient.GetAsync(Url(booked.Id, "/messages"), Ct)).ReadAsync<PagedResult<ConsultationMessageDto>>();
        page.Total.ShouldBe(2);
        page.Items.Select(m => m.SenderRole).ShouldBe([UserRole.Patient, UserRole.Doctor]);
    }

    [Fact]
    public async Task Ready_for_next_offers_the_next_patient_an_early_start()
    {
        AdvanceToNextLocal(8);
        var doctor = await Factory.BookableDoctorAsync();
        var first = await Factory.PatientAsync();
        var next = await Factory.PatientAsync();
        var today = Factory.Today();
        var current = await first.PaidAsync(doctor.DoctorId, LocalTime(today, 9));
        var later = await next.PaidAsync(doctor.DoctorId, LocalTime(today, 10));

        AdvanceTo(current.StartAt);
        var doctorClient = await Factory.ClientForAsync(doctor.UserId);
        var nextClient = await Factory.ClientForAsync(next.UserId);
        (await doctorClient.PostAsync(Url(current.Id, "/ready-for-next"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await (await Factory.ClientForAsync(first.UserId)).JoinedAsync(current.Id);
        await doctorClient.JoinedAsync(current.Id);
        await doctorClient.AdmittedAsync(current.Id);
        (await nextClient.GetAsync(Url(later.Id, "/early-join"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await nextClient.JoinAsync(later.Id)).ProblemCodeAsync()).ShouldBe("too_early");

        var offered = await (await doctorClient.PostAsync(Url(current.Id, "/ready-for-next"), null, Ct)).ReadAsync<ReadyForNextDto>();

        offered.Status.ShouldBe(ReadyForNextStatus.Offered);
        offered.AppointmentId.ShouldBe(later.Id);
        (await (await doctorClient.PostAsync(Url(current.Id, "/ready-for-next"), null, Ct)).ReadAsync<ReadyForNextDto>())
            .Status.ShouldBe(ReadyForNextStatus.AlreadyOffered);
        (await Fixture.ScalarAsync<string>($"SELECT body FROM notifications WHERE template_key = 'early_join_offered' AND user_id = '{next.UserId}' AND channel = 'email'"))
            .ShouldNotBeNull().ShouldContain($"https://app.telemed.test/appointments/{later.Id}/consultation");
        (await (await Factory.ClientForAsync(first.UserId)).PostAsync(Url(current.Id, "/ready-for-next"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await (await nextClient.GetAsync(Url(later.Id, "/early-join"), Ct)).ReadAsync<EarlyJoinDto>()).Response.ShouldBeNull();
        (await (await nextClient.PostAsync(Url(later.Id, "/early-join/accept"), null, Ct)).ReadAsync<EarlyJoinDto>()).Response.ShouldBe(EarlyJoinResponse.Accepted);
        (await nextClient.PostAsync(Url(later.Id, "/early-join/accept"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await nextClient.PostAsync(Url(later.Id, "/early-join/decline"), null, Ct)).ProblemCodeAsync()).ShouldBe("early_join_answered");

        (await nextClient.JoinedAsync(later.Id)).Status.ShouldBe(ConsultationStatus.Waiting);
        (await (await nextClient.GetAsync(Url(later.Id, "/waiting-room"), Ct)).ReadAsync<WaitingRoomDto>())
            .ShouldBe(new WaitingRoomDto(true, 2, 1, 30 * 60));
        (await (await doctorClient.PostAsync(Url(current.Id, "/ready-for-next"), null, Ct)).ReadAsync<ReadyForNextDto>())
            .Status.ShouldBe(ReadyForNextStatus.AlreadyWaiting);
    }

    [Fact]
    public async Task Sweep_marks_a_patient_who_never_joined_as_a_no_show()
    {
        var (doctor, _, booked) = await PaidAppointmentAsync();
        AdvanceTo(booked.StartAt);
        await (await Factory.ClientForAsync(doctor.UserId)).JoinedAsync(booked.Id);

        AdvanceTo(booked.EndAt - TimeSpan.FromSeconds(1));
        await Factory.RunJobAsync<ConsultationSweepJob>();
        (await AppointmentStateAsync(booked.Id)).ShouldBe("confirmed");

        Factory.Time.Advance(TimeSpan.FromSeconds(1));
        await Factory.RunJobAsync<ConsultationSweepJob>();

        (await AppointmentStateAsync(booked.Id)).ShouldBe("no_show:77");
        (await Fixture.ScalarAsync<string>($"SELECT concat_ws(':', status, end_reason) FROM consultations WHERE appointment_id = '{booked.Id}'"))
            .ShouldBe("abandoned:patient_no_show");
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM admin_notifications WHERE kind = 'doctor_no_show'")).ShouldBe(0);

        await Factory.SettleAsync();
        (await PaymentStatusAsync(booked.Id)).ShouldBe("partially_refunded");
        (await Fixture.ScalarAsync<string>(
                $"SELECT concat_ws(':', reason, amount_cents, payout_cents) FROM refunds WHERE payment_id = (SELECT id FROM payments WHERE appointment_id = '{booked.Id}')"))
            .ShouldBe($"patient_no_show:{FinanceFlows.DoctorShare}:{FinanceFlows.DoctorShare}");
        (await Fixture.ScalarAsync<Guid?>($"SELECT payout_id FROM payments WHERE appointment_id = '{booked.Id}'")).ShouldBeNull();
    }

    [Fact]
    public async Task Sweep_cancels_with_a_full_refund_and_alerts_admins_when_the_doctor_never_joined()
    {
        var (doctor, patient, booked) = await PaidAppointmentAsync();
        var nobody = await (await Factory.PatientAsync()).PaidAsync(doctor.DoctorId, await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(1)));
        AdvanceTo(booked.StartAt);
        await (await Factory.ClientForAsync(patient.UserId)).JoinedAsync(booked.Id);

        AdvanceTo(new[] { booked.EndAt, nobody.EndAt }.Max());
        await Factory.RunJobAsync<ConsultationSweepJob>();

        foreach (var id in new[] { booked.Id, nobody.Id })
        {
            (await Fixture.ScalarAsync<string>($"SELECT concat_ws(':', status, cancelled_by, cancellation_reason, refund_percent) FROM appointments WHERE id = '{id}'"))
                .ShouldBe("cancelled:system:doctor_no_show:100");
            (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM admin_notifications WHERE kind = 'doctor_no_show' AND resource_id = '{id}'")).ShouldBe(1);
        }

        (await Fixture.ScalarAsync<string>($"SELECT concat_ws(':', status, end_reason) FROM consultations WHERE appointment_id = '{booked.Id}'"))
            .ShouldBe("abandoned:doctor_no_show");
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM consultations WHERE appointment_id = '{nobody.Id}'")).ShouldBe(0);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM notifications WHERE template_key = 'appointment_cancelled' AND user_id = '{patient.UserId}'"))
            .ShouldBeGreaterThan(0);

        await Factory.SettleAsync();
        (await PaymentStatusAsync(booked.Id)).ShouldBe("voided");
        (await PaymentStatusAsync(nobody.Id)).ShouldBe("voided");
    }

    [Fact]
    public async Task Sweep_warns_the_next_patient_once_when_the_doctor_runs_over()
    {
        var (doctor, patient, current) = await PaidAppointmentAsync();
        var nextPatient = await Factory.PatientAsync();
        var next = await nextPatient.PaidAsync(doctor.DoctorId, current.EndAt);
        AdvanceTo(current.StartAt);
        await (await Factory.ClientForAsync(patient.UserId)).JoinedAsync(current.Id);
        var doctorClient = await Factory.ClientForAsync(doctor.UserId);
        await doctorClient.JoinedAsync(current.Id);
        await doctorClient.AdmittedAsync(current.Id);

        AdvanceTo(current.EndAt - TimeSpan.FromMinutes(1));
        await Factory.RunJobAsync<ConsultationSweepJob>();
        (await RunningLateCountAsync(nextPatient.UserId)).ShouldBe(0);

        AdvanceTo(current.EndAt + TimeSpan.FromMinutes(1));
        await Factory.RunJobAsync<ConsultationSweepJob>();
        await Factory.RunJobAsync<ConsultationSweepJob>();

        (await RunningLateCountAsync(nextPatient.UserId)).ShouldBe(1);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM consultations WHERE appointment_id = '{current.Id}' AND running_late_notified_at IS NOT NULL"))
            .ShouldBe(1);
        (await AppointmentStateAsync(next.Id)).ShouldBe("confirmed");
    }

    [Fact]
    public async Task Sweep_ends_an_active_call_once_nobody_has_been_connected_for_two_hours()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var id = await patient.Client.InstantMeetingAsync(new { counterpartUserId = doctor.UserId });
        var joined = await patient.Client.JoinedAsync(id);
        await doctor.Client.JoinedAsync(id);
        await doctor.Client.AdmittedAsync(id);

        await using (var peer = await Factory.ConnectAsync(joined.RoomToken))
        {
            await peer.NextAsync("welcome");
            Factory.Time.Advance(PlatformPolicy.StaleConsultationAfter + TimeSpan.FromMinutes(1));
            await Factory.RunJobAsync<ConsultationSweepJob>();
            (await ConsultationStateAsync(id)).ShouldBe("active");
        }

        await WaitForAsync(async () => await Fixture.ScalarAsync<long>("SELECT count(*) FROM consultation_events WHERE kind = 'left'") == 1);
        Factory.Time.Advance(PlatformPolicy.StaleConsultationAfter - TimeSpan.FromMinutes(1));
        await Factory.RunJobAsync<ConsultationSweepJob>();
        (await ConsultationStateAsync(id)).ShouldBe("active");

        Factory.Time.Advance(TimeSpan.FromMinutes(2));
        await Factory.RunJobAsync<ConsultationSweepJob>();

        (await ConsultationStateAsync(id)).ShouldBe("ended:stale");
        (await AppointmentStateAsync(id)).ShouldBe("completed");
    }

    [Fact]
    public async Task Sweep_abandons_a_room_nobody_was_admitted_to_once_the_doctor_join_window_closes()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var id = await patient.Client.InstantMeetingAsync(new { counterpartUserId = doctor.UserId });
        await patient.Client.JoinedAsync(id);
        await doctor.Client.JoinedAsync(id);
        var appointment = await patient.Client.AppointmentAsync(id);

        AdvanceTo(appointment.EndAt + PlatformPolicy.DoctorJoinGraceAfterEnd - TimeSpan.FromMinutes(1));
        await Factory.RunJobAsync<ConsultationSweepJob>();
        (await ConsultationStateAsync(id)).ShouldBe("waiting");

        Factory.Time.Advance(TimeSpan.FromMinutes(1));
        await Factory.RunJobAsync<ConsultationSweepJob>();

        (await ConsultationStateAsync(id)).ShouldBe($"abandoned:{PlatformPolicy.ConsultationStaleReason}");
        (await AppointmentStateAsync(id)).ShouldBe("confirmed");
    }

    [Fact]
    public async Task Sweep_abandons_a_waiting_room_whose_appointment_was_cancelled()
    {
        var (_, patient, booked) = await PaidAppointmentAsync();
        AdvanceTo(booked.StartAt - TimeSpan.FromMinutes(10));
        var patientClient = await Factory.ClientForAsync(patient.UserId);
        await patientClient.JoinedAsync(booked.Id);
        await Fixture.ExecuteSqlAsync($"UPDATE appointments SET status = 'cancelled', cancelled_at = now() WHERE id = '{booked.Id}'");

        await Factory.RunJobAsync<ConsultationSweepJob>();

        (await ConsultationStateAsync(booked.Id)).ShouldBe($"abandoned:{PlatformPolicy.ConsultationStaleReason}");
    }

    [Fact]
    public async Task Confirmed_appointments_complete_themselves_12_hours_after_the_end()
    {
        var (_, _, booked) = await PaidAppointmentAsync();

        AdvanceTo(booked.EndAt + PlatformPolicy.AutoCompleteAfter - TimeSpan.FromMinutes(1));
        await Factory.RunJobAsync<AutoCompleteAppointmentsJob>();
        (await AppointmentStateAsync(booked.Id)).ShouldBe("confirmed");

        Factory.Time.Advance(TimeSpan.FromMinutes(2));
        await Factory.RunJobAsync<AutoCompleteAppointmentsJob>();
        (await AppointmentStateAsync(booked.Id)).ShouldBe("completed");
    }

    private async Task<(BookableDoctor Doctor, Patient Patient, AppointmentDto Appointment)> PaidAppointmentAsync()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var booked = await patient.PaidAsync(doctor.DoctorId, await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(1)));
        return (doctor, patient, booked);
    }

    private void AdvanceTo(DateTimeOffset instant) => Factory.Time.Advance(instant - Factory.Time.GetUtcNow());

    private void AdvanceToNextLocal(int hour)
    {
        var target = LocalTime(Factory.Today(), hour);
        AdvanceTo(target > Factory.Time.GetUtcNow() ? target : LocalTime(Factory.Today().AddDays(1), hour));
    }

    private Task<string?> PaymentStatusAsync(Guid appointmentId) =>
        Fixture.ScalarAsync<string>($"SELECT status FROM payments WHERE appointment_id = '{appointmentId}'");

    private Task<string?> AppointmentStateAsync(Guid appointmentId) =>
        Fixture.ScalarAsync<string>($"SELECT concat_ws(':', status, refund_percent) FROM appointments WHERE id = '{appointmentId}'");

    private Task<string?> ConsultationStateAsync(Guid appointmentId) =>
        Fixture.ScalarAsync<string>($"SELECT concat_ws(':', status, end_reason) FROM consultations WHERE appointment_id = '{appointmentId}'");

    private Task<long> RunningLateCountAsync(Guid userId) =>
        Fixture.ScalarAsync<long>($"SELECT count(*) FROM notifications WHERE template_key = 'doctor_running_late' AND user_id = '{userId}'");

    private static async Task WaitForAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 100 && !await condition(); attempt++)
        {
            await Task.Delay(100, Ct);
        }

        (await condition()).ShouldBeTrue();
    }
}
