using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;

namespace TeleMed.Api.IntegrationTests;

public class BookingTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Booking_takes_the_slot_immediately_and_snapshots_fee_and_patient()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var start = await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3));

        var booked = await patient.Client.BookedAsync(doctor.DoctorId, start);

        booked.Status.ShouldBe(AppointmentStatus.PendingPayment);
        (booked.StartAt, booked.EndAt).ShouldBe((start, start.AddMinutes(30)));
        booked.FeeCents.ShouldBe(250_000);
        booked.PaymentDueAt.ShouldBe(Factory.Time.GetUtcNow() + PlatformPolicy.PaymentWindow);
        booked.VisitPatient.ShouldBe(new VisitPatientDto("Kamal Silva", new DateOnly(1990, 4, 1), Sex.Male, 70.5m, "Penicillin"));
        booked.Intake.Symptoms.ShouldBe("Fever for two days");
        (await Factory.SlotsAsync(doctor.DoctorId)).Slots.Single(s => s.StartAt == start).Available.ShouldBeFalse();

        var payment = await Fixture.ScalarAsync<string>(
            $"SELECT concat_ws(',', status, gross_cents, amount_cents, commission_cents, provider_fee_cents, payout_cents) FROM payments WHERE appointment_id = '{booked.Id}'");
        payment.ShouldBe("pending,250000,250000,50000,7500,192500");
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM audit_logs WHERE entity_type = 'appointments' AND entity_id = '{booked.Id}'")).ShouldBe(1);
    }

    [Fact]
    public async Task Twenty_parallel_bookings_for_one_slot_give_exactly_one_success()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var start = await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3));
        var patients = new List<Patient>();
        for (var i = 0; i < 20; i++)
        {
            patients.Add(await Factory.PatientAsync());
        }

        var responses = await Task.WhenAll(patients.Select(p => p.Client.BookAsync(doctor.DoctorId, start)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        foreach (var rejected in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            rejected.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await rejected.ProblemCodeAsync()).ShouldBe("slot_unavailable");
        }

        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM appointments WHERE doctor_id = '{doctor.DoctorId}'")).ShouldBe(1);
    }

    [Fact]
    public async Task A_patient_cannot_hold_overlapping_appointments_with_different_doctors()
    {
        var first = await Factory.BookableDoctorAsync();
        var second = await Factory.BookableDoctorAsync(SecondDoctor);
        var patient = await Factory.PatientAsync();
        var start = await Factory.FreeSlotAsync(first.DoctorId, TimeSpan.FromHours(3));
        await patient.Client.BookedAsync(first.DoctorId, start);

        var overlap = await patient.Client.BookAsync(second.DoctorId, start);

        overlap.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await overlap.ProblemCodeAsync()).ShouldBe("patient_overlap");
        (await Factory.SlotsAsync(second.DoctorId)).Slots.Single(s => s.StartAt == start).Available.ShouldBeTrue();
    }

    [Fact]
    public async Task Only_available_grid_starts_can_be_booked()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var start = await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3));

        var offGrid = await patient.Client.BookAsync(doctor.DoctorId, start.AddMinutes(10));
        var past = await patient.Client.BookAsync(doctor.DoctorId, start.AddDays(-2));
        var tooFar = await patient.Client.BookAsync(doctor.DoctorId, start.AddDays(40));

        foreach (var response in new[] { offGrid, past, tooFar })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await response.ProblemCodeAsync()).ShouldBe("slot_unavailable");
        }

        (await patient.Client.BookAsync(Guid.NewGuid(), start)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await patient.Client.PostJsonAsync("/api/v1/appointments", new { doctorId = doctor.DoctorId, startAt = start })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await doctor.Client.BookAsync(doctor.DoctorId, start)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Doctors_not_accepting_new_patients_cannot_be_booked()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var start = await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3));
        await Fixture.ExecuteSqlAsync($"UPDATE doctors SET accepts_new_patients = false WHERE id = '{doctor.DoctorId}'");

        var response = await patient.Client.BookAsync(doctor.DoctorId, start);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe("not_accepting_patients");
    }

    [Fact]
    public async Task Participants_see_their_appointments_and_nobody_else_does()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var other = await Factory.BookableDoctorAsync(SecondDoctor);
        var patient = await Factory.PatientAsync();
        var stranger = await Factory.PatientAsync();
        var booked = await patient.Client.BookedAsync(doctor.DoctorId, await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3)));

        (await patient.Client.AppointmentAsync(booked.Id)).Id.ShouldBe(booked.Id);
        (await doctor.Client.AppointmentAsync(booked.Id)).Id.ShouldBe(booked.Id);
        (await stranger.Client.GetAsync($"/api/v1/appointments/{booked.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.Client.GetAsync($"/api/v1/appointments/{booked.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.Client.PostAsync($"/api/v1/appointments/{booked.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await (await patient.Client.GetAsync("/api/v1/appointments", Ct)).ReadAsync<PagedResult<AppointmentDto>>()).Items.Select(a => a.Id).ShouldBe([booked.Id]);
        (await (await doctor.Client.GetAsync("/api/v1/appointments?status=pendingPayment", Ct)).ReadAsync<PagedResult<AppointmentDto>>()).Total.ShouldBe(1);
        (await (await doctor.Client.GetAsync("/api/v1/appointments?status=confirmed", Ct)).ReadAsync<PagedResult<AppointmentDto>>()).Total.ShouldBe(0);
        (await (await other.Client.GetAsync("/api/v1/appointments", Ct)).ReadAsync<PagedResult<AppointmentDto>>()).Total.ShouldBe(0);
        (await (await stranger.Client.GetAsync("/api/v1/appointments", Ct)).ReadAsync<PagedResult<AppointmentDto>>()).Total.ShouldBe(0);
    }

    [Fact]
    public async Task Last_visit_details_come_from_the_patients_own_latest_visit()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var empty = await (await patient.Client.GetAsync("/api/v1/appointments/last-visit-details", Ct)).ReadAsync<LastVisitDetailsDto>();
        empty.WeightKg.ShouldBeNull();

        var first = await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3));
        await patient.Client.PostJsonAsync("/api/v1/appointments", BookingBody(doctor.DoctorId, first, weightKg: 70.5m));
        Factory.Time.Advance(TimeSpan.FromSeconds(1));
        await patient.Client.PostJsonAsync("/api/v1/appointments", BookingBody(doctor.DoctorId, first.AddHours(1), weightKg: 12m, visitRelation: "child"));

        var details = await (await patient.Client.GetAsync("/api/v1/appointments/last-visit-details", Ct)).ReadAsync<LastVisitDetailsDto>();

        details.ShouldBe(new LastVisitDetailsDto("Kamal Silva", new DateOnly(1990, 4, 1), Sex.Male, 70.5m, "Penicillin"));
    }

    [Fact]
    public async Task Unpaid_bookings_expire_and_free_the_slot()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var start = await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3));
        var booked = await patient.Client.BookedAsync(doctor.DoctorId, start);

        await Factory.ExpireUnpaidAsync();
        (await Fixture.ScalarAsync<string>($"SELECT status FROM appointments WHERE id = '{booked.Id}'")).ShouldBe("pending_payment");

        Factory.Time.Advance(PlatformPolicy.PaymentWindow + TimeSpan.FromSeconds(1));
        await Factory.ExpireUnpaidAsync();

        var expired = await (await Factory.ClientForAsync(patient.UserId)).AppointmentAsync(booked.Id);
        expired.Status.ShouldBe(AppointmentStatus.Cancelled);
        expired.CancelledBy.ShouldBe(CancellationActor.System);
        expired.CancellationReason.ShouldBe(PlatformPolicy.PaymentTimeoutReason);
        (await Fixture.ScalarAsync<string>($"SELECT status || ':' || failure_reason FROM payments WHERE appointment_id = '{booked.Id}'")).ShouldBe("failed:payment_timeout");
        (await Factory.SlotsAsync(doctor.DoctorId)).Slots.Single(s => s.StartAt == start).Available.ShouldBeTrue();
    }

    [Fact]
    public async Task Doctors_complete_or_mark_no_show_only_after_the_start()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var first = await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromMinutes(40));
        var a = await patient.Client.BookedAsync(doctor.DoctorId, first);
        var b = await patient.Client.BookedAsync(doctor.DoctorId, first.AddMinutes(30));
        var pending = await patient.Client.BookedAsync(doctor.DoctorId, first.AddMinutes(60));
        await patient.Client.PayWithMockAsync(a.Id);
        await patient.Client.PayWithMockAsync(b.Id);

        var early = await doctor.Client.PostAsync($"/api/v1/appointments/{a.Id}/complete", null, Ct);
        early.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await early.ProblemCodeAsync()).ShouldBe("not_started");
        (await patient.Client.PostAsync($"/api/v1/appointments/{a.Id}/complete", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        Factory.Time.Advance(first.AddMinutes(95) - Factory.Time.GetUtcNow());
        var doctorClient = await Factory.ClientForAsync(doctor.UserId);
        var completed = await (await doctorClient.PostAsync($"/api/v1/appointments/{a.Id}/complete", null, Ct)).ReadAsync<AppointmentDto>();
        var noShow = await (await doctorClient.PostAsync($"/api/v1/appointments/{b.Id}/no-show", null, Ct)).ReadAsync<AppointmentDto>();
        var unpaid = await doctorClient.PostAsync($"/api/v1/appointments/{pending.Id}/complete", null, Ct);

        completed.Status.ShouldBe(AppointmentStatus.Completed);
        noShow.Status.ShouldBe(AppointmentStatus.NoShow);
        noShow.RefundPercent.ShouldBe(77);
        unpaid.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await unpaid.ProblemCodeAsync()).ShouldBe("invalid_transition");
        (await (await doctorClient.PostAsync($"/api/v1/appointments/{a.Id}/cancel", null, Ct)).ProblemCodeAsync()).ShouldBe("invalid_transition");

        await Factory.SettleAsync();

        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM payments WHERE status = 'succeeded' AND captured_cents = 250000 AND doctor_id = '{doctor.DoctorId}'"))
            .ShouldBe(1);
        (await Fixture.ScalarAsync<string>(
                $"SELECT status FROM payments WHERE appointment_id = '{b.Id}'"))
            .ShouldBe("partially_refunded");
        (await Fixture.ScalarAsync<long>(
                $"SELECT count(*) FROM refunds WHERE payment_id = (SELECT id FROM payments WHERE appointment_id = '{b.Id}') AND reason = 'patient_no_show'"))
            .ShouldBe(1);
    }
}
