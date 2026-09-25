using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.Appointments;
using TeleMed.Application.Admin.Audit;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Reschedules;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Persistence;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;

namespace TeleMed.Api.IntegrationTests;

public class AdminAppointmentsTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> TestAppointmentAsync(Guid doctorId, Guid patientId, DateTimeOffset start)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var appointment = new Appointment
        {
            PatientId = patientId,
            DoctorId = doctorId,
            StartAt = start,
            EndAt = start.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            IsTest = true,
            VisitPatientName = "Test Visit",
            VisitPatientDateOfBirth = new DateOnly(1990, 1, 1),
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(Ct);
        return appointment.Id;
    }

    [Fact]
    public async Task Admins_list_and_filter_appointments_including_test_ones_on_request()
    {
        var first = await Factory.BookableDoctorAsync();
        var second = await Factory.BookableDoctorAsync(SecondDoctor);
        var patient = await Factory.PatientAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Support);
        var day = Factory.Today().AddDays(2);
        var confirmed = await patient.PaidAsync(first.DoctorId, LocalTime(day, 9));
        var pending = await patient.Client.BookedAsync(second.DoctorId, LocalTime(day, 11));
        var test = await TestAppointmentAsync(first.DoctorId, patient.UserId, LocalTime(day, 13));

        async Task<List<Guid>> ListAsync(string query) =>
            (await (await admin.GetAsync($"/api/v1/admin/appointments{query}", Ct)).ReadAsync<PagedResult<AppointmentDto>>()).Items.Select(a => a.Id).ToList();

        (await ListAsync("")).ShouldBe([pending.Id, confirmed.Id], ignoreOrder: true);
        (await ListAsync("?includeTest=true")).ShouldBe([pending.Id, confirmed.Id, test], ignoreOrder: true);
        (await ListAsync($"?doctorId={first.DoctorId}")).ShouldBe([confirmed.Id]);
        (await ListAsync("?status=pendingPayment")).ShouldBe([pending.Id]);
        (await ListAsync($"?patientId={patient.UserId}&from={LocalTime(day, 10):O}".Replace("+", "%2B"))).ShouldBe([pending.Id]);
        (await ListAsync($"?patientId={Guid.NewGuid()}")).ShouldBeEmpty();
        (await admin.GetAsync("/api/v1/admin/appointments?page=0", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var detail = await (await admin.GetAsync($"/api/v1/admin/appointments/{confirmed.Id}", Ct)).ReadAsync<AdminAppointmentDetailDto>();
        detail.Appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
        detail.Payment!.Status.ShouldBe(PaymentStatus.Authorized);
        detail.RescheduleRequests.ShouldBeEmpty();
        (await admin.GetAsync($"/api/v1/admin/appointments/{Guid.NewGuid()}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_cancel_refunds_in_full_and_the_audit_trail_covers_the_whole_appointment()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var adminUser = await Factory.CreateAdminAsync(AdminRole.Ops);
        var admin = Factory.CreateAdminClient(Factory.LocalAdminToken(adminUser.Email));
        var booked = await patient.PaidAsync(doctor.DoctorId, LocalTime(Factory.Today().AddDays(8), 10));
        await doctor.Client.ProposedAsync(booked.Id, LocalTime(Factory.Today().AddDays(8), 15));

        (await admin.PostJsonAsync($"/api/v1/admin/appointments/{booked.Id}/cancel", new { reason = "" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var cancelled = await (await admin.PostJsonAsync($"/api/v1/admin/appointments/{booked.Id}/cancel", new { reason = "Duplicate booking" }))
            .ReadAsync<AppointmentDto>();

        cancelled.Status.ShouldBe(AppointmentStatus.Cancelled);
        cancelled.CancelledBy.ShouldBe(CancellationActor.Admin);
        cancelled.RefundPercent.ShouldBe(100);
        (await Fixture.ScalarAsync<string>("SELECT concat_ws(':', status, reason, percent) FROM refunds")).ShouldBe("processing:admin_cancellation:100");

        var audit = await (await admin.GetAsync($"/api/v1/admin/appointments/{booked.Id}/audit", Ct)).ReadAsync<List<AuditEntryDto>>();
        audit.Select(a => a.EntityType).Distinct().ShouldBe(["appointments", "payments", "reschedule_requests", "refunds"], ignoreOrder: true);
        var cancel = audit.Last(a => a.EntityType == "appointments");
        (cancel.ActorType, cancel.ActorId, cancel.Action).ShouldBe((AuditActorType.Admin, adminUser.Id, "updated"));
        cancel.Changes.GetProperty("status").GetProperty("new").GetString().ShouldBe("cancelled");
        audit.ShouldBe(audit.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToList());
    }

    [Fact]
    public async Task Admins_work_the_reschedule_queue()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Support);
        var day = Factory.Today().AddDays(2);
        var first = await patient.PaidAsync(doctor.DoctorId, LocalTime(day, 9));
        var second = await patient.PaidAsync(doctor.DoctorId, LocalTime(day, 10));
        var toAccept = await doctor.Client.ProposedAsync(first.Id, LocalTime(day, 14));
        var toDecline = await doctor.Client.ProposedAsync(second.Id, LocalTime(day, 15));

        var queue = await (await admin.GetAsync("/api/v1/admin/reschedule-requests?status=pending", Ct)).ReadAsync<PagedResult<RescheduleRequestDto>>();
        queue.Items.Select(r => r.Id).ShouldBe([toAccept.Id, toDecline.Id]);

        var accepted = await (await admin.PostAsync($"/api/v1/admin/reschedule-requests/{toAccept.Id}/accept", null, Ct)).ReadAsync<RescheduleDecisionDto>();
        var declined = await (await admin.PostAsync($"/api/v1/admin/reschedule-requests/{toDecline.Id}/decline", null, Ct)).ReadAsync<RescheduleDecisionDto>();

        (accepted.Request.DecidedBy, accepted.Appointment.StartAt).ShouldBe((CancellationActor.Admin, LocalTime(day, 14)));
        (declined.Appointment.Status, declined.Appointment.CancelledBy, declined.Appointment.RefundPercent)
            .ShouldBe((AppointmentStatus.Cancelled, CancellationActor.Admin, 100));
        (await (await admin.GetAsync("/api/v1/admin/reschedule-requests?status=pending", Ct)).ReadAsync<PagedResult<RescheduleRequestDto>>()).Total.ShouldBe(0);
        (await (await admin.GetAsync("/api/v1/admin/reschedule-requests", Ct)).ReadAsync<PagedResult<RescheduleRequestDto>>()).Total.ShouldBe(2);
        (await patient.Client.PostAsync($"/api/v1/reschedule-requests/{toAccept.Id}/accept", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await (await Factory.PatientAsync()).Client.PostAsync($"/api/v1/reschedule-requests/{toAccept.Id}/accept", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
