using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Appointments;
using TeleMed.Application.Reschedules;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;

namespace TeleMed.Api.IntegrationTests;

public class RescheduleTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_proposal_holds_its_slot_at_once_and_accepting_moves_the_appointment()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var day = Factory.Today().AddDays(2);
        var (original, proposed) = (LocalTime(day, 10), LocalTime(day, 14));
        var booked = await patient.PaidAsync(doctor.DoctorId, original);

        var request = await doctor.Client.ProposedAsync(booked.Id, proposed);

        (request.Status, request.ProposedStartAt, request.ProposedEndAt).ShouldBe((RescheduleStatus.Pending, proposed, proposed.AddMinutes(30)));
        (await Factory.IsAvailableAsync(doctor.DoctorId, proposed)).ShouldBeFalse();
        var rival = await (await Factory.PatientAsync()).Client.BookAsync(doctor.DoctorId, proposed);
        (await rival.ProblemCodeAsync()).ShouldBe("slot_unavailable");
        (await (await doctor.Client.ProposeAsync(booked.Id, LocalTime(day, 15))).ProblemCodeAsync()).ShouldBe("reschedule_pending");
        (await (await patient.Client.GetAsync($"/api/v1/appointments/{booked.Id}/reschedule-requests", Ct)).ReadAsync<List<RescheduleRequestDto>>())
            .Select(r => r.Id).ShouldBe([request.Id]);
        (await (await Factory.PatientAsync()).Client.GetAsync($"/api/v1/appointments/{booked.Id}/reschedule-requests", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var accepted = await (await patient.Client.PostAsync($"/api/v1/reschedule-requests/{request.Id}/accept", null, Ct)).ReadAsync<RescheduleDecisionDto>();

        accepted.Request.Status.ShouldBe(RescheduleStatus.Accepted);
        accepted.Request.DecidedBy.ShouldBe(CancellationActor.Patient);
        (accepted.Appointment.StartAt, accepted.Appointment.EndAt).ShouldBe((proposed, proposed.AddMinutes(30)));
        accepted.Appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
        (await Factory.IsAvailableAsync(doctor.DoctorId, original)).ShouldBeTrue();
        (await Factory.IsAvailableAsync(doctor.DoctorId, proposed)).ShouldBeFalse();
        (await (await patient.Client.PostAsync($"/api/v1/reschedule-requests/{request.Id}/decline", null, Ct)).ProblemCodeAsync()).ShouldBe("reschedule_not_pending");
    }

    [Fact]
    public async Task Proposals_must_be_future_free_for_the_doctor_and_free_for_the_patient()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var other = await Factory.BookableDoctorAsync(SecondDoctor);
        var patient = await Factory.PatientAsync();
        var day = Factory.Today().AddDays(2);
        var booked = await patient.PaidAsync(doctor.DoctorId, LocalTime(day, 10));
        await patient.PaidAsync(other.DoctorId, LocalTime(day, 14));
        await (await Factory.PatientAsync()).PaidAsync(doctor.DoctorId, LocalTime(day, 16));

        var clash = await doctor.Client.ProposeAsync(booked.Id, LocalTime(day, 14, 15));
        clash.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await clash.ProblemCodeAsync()).ShouldBe("patient_overlap");
        (await (await doctor.Client.ProposeAsync(booked.Id, LocalTime(day, 16, 15))).ProblemCodeAsync()).ShouldBe("slot_unavailable");
        (await doctor.Client.ProposeAsync(booked.Id, Factory.Time.GetUtcNow().AddMinutes(-5))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await doctor.Client.ProposeAsync(booked.Id, LocalTime(day, 10))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await other.Client.ProposeAsync(booked.Id, LocalTime(day, 18))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await patient.Client.ProposeAsync(booked.Id, LocalTime(day, 18))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        (await doctor.Client.ProposedAsync(booked.Id, LocalTime(day, 10, 15))).ProposedStartAt.ShouldBe(LocalTime(day, 10, 15));
    }

    [Fact]
    public async Task Declining_cancels_with_a_full_refund_even_inside_the_late_cancellation_window()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var start = await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromMinutes(40));
        var booked = await patient.PaidAsync(doctor.DoctorId, start);
        var request = await doctor.Client.ProposedAsync(booked.Id, start.AddHours(3));

        var declined = await (await patient.Client.PostAsync($"/api/v1/reschedule-requests/{request.Id}/decline", null, Ct)).ReadAsync<RescheduleDecisionDto>();

        declined.Request.Status.ShouldBe(RescheduleStatus.Declined);
        declined.Appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
        declined.Appointment.CancelledBy.ShouldBe(CancellationActor.Patient);
        declined.Appointment.CancellationReason.ShouldBe("reschedule_declined");
        declined.Appointment.RefundPercent.ShouldBe(100);
        (await Factory.IsAvailableAsync(doctor.DoctorId, start.AddHours(3))).ShouldBeTrue();
        await Factory.SettleAsync();
        (await Fixture.ScalarAsync<string>($"SELECT status FROM payments WHERE appointment_id = '{booked.Id}'")).ShouldBe("voided");
    }

    [Fact]
    public async Task Unanswered_proposals_expire_when_the_original_time_passes_and_refund_in_full()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var day = Factory.Today().AddDays(8);
        var booked = await patient.PaidAsync(doctor.DoctorId, LocalTime(day, 10));
        var request = await doctor.Client.ProposedAsync(booked.Id, LocalTime(day, 15));

        await Factory.ExpireReschedulesAsync();
        (await Fixture.ScalarAsync<string>($"SELECT status FROM reschedule_requests WHERE id = '{request.Id}'")).ShouldBe("pending");

        Factory.Time.Advance(LocalTime(day, 10) - Factory.Time.GetUtcNow());
        await Factory.ExpireReschedulesAsync();

        (await Fixture.ScalarAsync<string>($"SELECT status || ':' || decided_by FROM reschedule_requests WHERE id = '{request.Id}'")).ShouldBe("expired:system");
        (await Fixture.ScalarAsync<string>($"SELECT concat_ws(':', status, cancelled_by, cancellation_reason, refund_percent) FROM appointments WHERE id = '{booked.Id}'"))
            .ShouldBe("cancelled:system:reschedule_expired:100");
        (await Fixture.ScalarAsync<string>("SELECT concat_ws(':', status, reason, percent, amount_cents) FROM refunds"))
            .ShouldBe("processing:reschedule_declined:100:250000");
    }

    [Fact]
    public async Task Cancelling_an_appointment_drops_its_pending_proposal()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var day = Factory.Today().AddDays(2);
        var booked = await patient.PaidAsync(doctor.DoctorId, LocalTime(day, 10));
        var request = await doctor.Client.ProposedAsync(booked.Id, LocalTime(day, 15));

        (await patient.Client.PostAsync($"/api/v1/appointments/{booked.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await Fixture.ScalarAsync<string>($"SELECT status || ':' || decided_by FROM reschedule_requests WHERE id = '{request.Id}'")).ShouldBe("expired:patient");
        (await Factory.IsAvailableAsync(doctor.DoctorId, LocalTime(day, 15))).ShouldBeTrue();
    }

    [Fact]
    public async Task Accepting_a_move_beyond_the_card_hold_marks_the_payment_for_capture()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var day = Factory.Today().AddDays(2);
        var near = await patient.PaidAsync(doctor.DoctorId, LocalTime(day, 10));
        var far = await patient.PaidAsync(doctor.DoctorId, LocalTime(day, 11));
        var state = (Guid id) => Fixture.ScalarAsync<string>($"SELECT status || ':' || (capture_requested_at IS NOT NULL) FROM payments WHERE appointment_id = '{id}'");

        foreach (var (appointment, to) in new[] { (near, LocalTime(day.AddDays(3), 10)), (far, LocalTime(day.AddDays(6), 10)) })
        {
            var request = await doctor.Client.ProposedAsync(appointment.Id, to);
            (await patient.Client.PostAsync($"/api/v1/reschedule-requests/{request.Id}/accept", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await state(near.Id)).ShouldBe("authorized:false");
        (await state(far.Id)).ShouldBe("authorized:true");

        await Factory.SettleAsync();

        (await state(near.Id)).ShouldBe("authorized:false");
        (await state(far.Id)).ShouldBe("succeeded:true");
        (await Fixture.ScalarAsync<long>($"SELECT captured_cents FROM payments WHERE appointment_id = '{far.Id}'")).ShouldBe(250_000);
    }
}
