using System.Net;
using System.Text.Json;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Scheduling;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;

namespace TeleMed.Api.IntegrationTests;

public class CalendarCancellationTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<int> AffectedAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, body);
        var root = JsonDocument.Parse(body).RootElement;
        root.GetProperty("code").GetString().ShouldBe("appointments_affected");
        return root.GetProperty("affectedAppointments").GetInt32();
    }

    private Task<string?> AppointmentStateAsync(Guid id) =>
        Fixture.ScalarAsync<string>($"SELECT concat_ws(':', status, cancelled_by, cancellation_reason, refund_percent) FROM appointments WHERE id = '{id}'");

    [Fact]
    public async Task Doctor_holiday_with_bookings_is_refused_until_confirmed_then_cancels_with_full_refunds()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var (alice, bob, carol) = (await Factory.PatientAsync(), await Factory.PatientAsync(), await Factory.PatientAsync());
        var day = Factory.Today().AddDays(8);
        var paid = await alice.PaidAsync(doctor.DoctorId, LocalTime(day, 10));
        var unpaid = await bob.Client.BookedAsync(doctor.DoctorId, LocalTime(day, 11));
        var elsewhere = await carol.PaidAsync(doctor.DoctorId, LocalTime(day.AddDays(1), 10));
        var proposal = await doctor.Client.ProposedAsync(elsewhere.Id, LocalTime(day, 12));

        (await AffectedAsync(await doctor.Client.PostJsonAsync("/api/v1/doctors/me/holidays", new { date = day, reason = "Leave" }))).ShouldBe(3);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM holidays")).ShouldBe(0);
        (await AppointmentStateAsync(paid.Id)).ShouldBe("confirmed");

        var holiday = await (await doctor.Client.PostJsonAsync("/api/v1/doctors/me/holidays", new { date = day, reason = "Leave", cancelBooked = true }))
            .ReadAsync<HolidayDto>(HttpStatusCode.Created);

        (await AppointmentStateAsync(paid.Id)).ShouldBe("cancelled:doctor:doctor_on_leave:100");
        (await AppointmentStateAsync(unpaid.Id)).ShouldBe("cancelled:doctor:doctor_on_leave");
        (await AppointmentStateAsync(elsewhere.Id)).ShouldBe("confirmed");
        (await Fixture.ScalarAsync<string>($"SELECT status || ':' || decided_by FROM reschedule_requests WHERE id = '{proposal.Id}'")).ShouldBe("expired:doctor");
        (await Fixture.ScalarAsync<string>("SELECT concat_ws(':', status, reason, percent, amount_cents) FROM refunds"))
            .ShouldBe("processing:doctor_cancellation:100:250000");
        (await Fixture.ScalarAsync<string>($"SELECT status FROM payments WHERE appointment_id = '{unpaid.Id}'")).ShouldBe("failed");

        (await doctor.Client.DeleteAsync($"/api/v1/doctors/me/holidays/{holiday.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Factory.IsAvailableAsync(doctor.DoctorId, LocalTime(day, 10))).ShouldBeTrue();
        (await Factory.IsAvailableAsync(doctor.DoctorId, LocalTime(day, 12))).ShouldBeTrue();
        (await AppointmentStateAsync(paid.Id)).ShouldBe("cancelled:doctor:doctor_on_leave:100");
    }

    [Fact]
    public async Task Platform_holiday_cancels_bookings_across_doctors()
    {
        var first = await Factory.BookableDoctorAsync();
        var second = await Factory.BookableDoctorAsync(SecondDoctor);
        var admin = await Factory.AdminClientAsync(AdminRole.Ops);
        var day = Factory.Today().AddDays(8);
        var withFirst = await (await Factory.PatientAsync()).PaidAsync(first.DoctorId, LocalTime(day, 9));
        var withSecond = await (await Factory.PatientAsync()).PaidAsync(second.DoctorId, LocalTime(day, 16));
        var otherDay = await (await Factory.PatientAsync()).PaidAsync(second.DoctorId, LocalTime(day.AddDays(1), 9));

        (await AffectedAsync(await admin.PostJsonAsync("/api/v1/admin/holidays", new { date = day, reason = "Poya" }))).ShouldBe(2);

        (await admin.PostJsonAsync("/api/v1/admin/holidays", new { date = day, reason = "Poya", cancelBooked = true })).StatusCode.ShouldBe(HttpStatusCode.Created);

        (await AppointmentStateAsync(withFirst.Id)).ShouldBe("cancelled:admin:doctor_on_leave:100");
        (await AppointmentStateAsync(withSecond.Id)).ShouldBe("cancelled:admin:doctor_on_leave:100");
        (await AppointmentStateAsync(otherDay.Id)).ShouldBe("confirmed");
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM refunds WHERE percent = 100 AND reason = 'admin_cancellation'")).ShouldBe(2);
    }

    [Fact]
    public async Task Slot_block_over_bookings_is_refused_until_confirmed_then_cancels_them()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Ops);
        var day = Factory.Today().AddDays(8);
        var inside = await (await Factory.PatientAsync()).PaidAsync(doctor.DoctorId, LocalTime(day, 10));
        var outside = await (await Factory.PatientAsync()).PaidAsync(doctor.DoctorId, LocalTime(day, 11));
        var block = new { startAt = LocalTime(day, 10, 15), endAt = LocalTime(day, 11), reason = "Maintenance" };

        (await AffectedAsync(await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctor.DoctorId}/slot-blocks", block))).ShouldBe(1);

        (await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctor.DoctorId}/slot-blocks", new { block.startAt, block.endAt, block.reason, cancelBooked = true }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        (await AppointmentStateAsync(inside.Id)).ShouldBe("cancelled:admin:slot_blocked:100");
        (await AppointmentStateAsync(outside.Id)).ShouldBe("confirmed");
        (await Fixture.ScalarAsync<string>("SELECT concat_ws(':', status, reason, percent) FROM refunds")).ShouldBe("processing:admin_cancellation:100");
        (await Factory.IsAvailableAsync(doctor.DoctorId, LocalTime(day, 10))).ShouldBeFalse();
    }

    [Fact]
    public async Task A_booking_waits_for_a_platform_holiday_in_progress_and_then_sees_it()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var day = Factory.Today().AddDays(2);

        await using var connection = await Fixture.OpenConnectionAsync();
        await using var holidayInProgress = await connection.BeginTransactionAsync(Ct);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT pg_advisory_xact_lock(hashtext('cal:platform'))";
            await command.ExecuteNonQueryAsync(Ct);
        }

        var booking = patient.Client.BookAsync(doctor.DoctorId, LocalTime(day, 10));
        await Task.Delay(TimeSpan.FromMilliseconds(500), Ct);
        booking.IsCompleted.ShouldBeFalse();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"INSERT INTO holidays (id, doctor_id, date, reason, created_at, updated_at) VALUES (gen_random_uuid(), NULL, '{day:yyyy-MM-dd}', 'Poya', now(), now())";
            await command.ExecuteNonQueryAsync(Ct);
        }

        await holidayInProgress.CommitAsync(Ct);
        (await (await booking).ProblemCodeAsync()).ShouldBe("slot_unavailable");
    }
}
