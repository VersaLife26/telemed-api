using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.Doctors;
using TeleMed.Application.Admin.Users;
using TeleMed.Application.Auth;
using TeleMed.Application.Common;
using TeleMed.Application.Users;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.FinanceFlows;

namespace TeleMed.Api.IntegrationTests;

public class AdminPlatformUserTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Suspension_cuts_live_sessions_login_refresh_and_booking_until_reinstated()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var auth = await Factory.CreateUserAsync(UserRole.Patient, "blocked@example.com", "a good password");
        var patient = Factory.CreateClient().WithBearer(auth.AccessToken);
        var slot = await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3));
        var admin = await Factory.AdminClientAsync(AdminRole.Support);

        var suspended = await (await admin.PostJsonAsync($"/api/v1/admin/users/{auth.User.Id}/suspend", new { reason = "Abusive messages" }))
            .ReadAsync<PlatformUserDetailDto>();
        suspended.Status.ShouldBe(UserStatus.Suspended);
        suspended.SuspendedReason.ShouldBe("Abusive messages");
        (await admin.PostJsonAsync($"/api/v1/admin/users/{auth.User.Id}/suspend", new { reason = "Again" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await patient.GetAsync("/api/v1/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await patient.BookAsync(doctor.DoctorId, slot)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Factory.CreateClient().PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken })).StatusCode
            .ShouldNotBe(HttpStatusCode.OK);
        var login = await Factory.CreateClient().PostJsonAsync("/api/v1/auth/login/email", new { email = "blocked@example.com", password = "a good password" });
        login.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await login.ProblemCodeAsync()).ShouldBe("account_suspended");
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM refresh_tokens WHERE user_id = '{auth.User.Id}' AND revoked_at IS NULL")).ShouldBe(0);

        var reinstated = await (await admin.PostAsync($"/api/v1/admin/users/{auth.User.Id}/reinstate", null, Ct)).ReadAsync<PlatformUserDetailDto>();
        reinstated.Status.ShouldBe(UserStatus.Active);
        reinstated.SuspendedAt.ShouldBeNull();

        var fresh = await (await Factory.CreateClient().PostJsonAsync("/api/v1/auth/login/email", new { email = "blocked@example.com", password = "a good password" }))
            .ReadAsync<AuthResponse>();
        var client = Factory.CreateClient().WithBearer(fresh.AccessToken);
        (await client.BookAsync(doctor.DoctorId, slot)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Admins_search_users_and_see_their_activity()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        await patient.Client.BookedAsync(doctor.DoctorId, await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3)));
        var admin = await Factory.AdminClientAsync(AdminRole.Ops);

        var doctors = await (await admin.GetAsync("/api/v1/admin/users?role=doctor", Ct)).ReadAsync<PagedResult<PlatformUserDto>>();
        doctors.Items.ShouldHaveSingleItem().DoctorId.ShouldBe(doctor.DoctorId);
        var byName = await (await admin.GetAsync("/api/v1/admin/users?q=nimal", Ct)).ReadAsync<PagedResult<PlatformUserDto>>();
        byName.Items.ShouldHaveSingleItem().Id.ShouldBe(doctor.UserId);

        var detail = await (await admin.GetAsync($"/api/v1/admin/users/{patient.UserId}", Ct)).ReadAsync<PlatformUserDetailDto>();
        detail.Role.ShouldBe(UserRole.Patient);
        detail.DoctorId.ShouldBeNull();

        var activity = await (await admin.GetAsync($"/api/v1/admin/users/{patient.UserId}/activity", Ct)).ReadAsync<UserActivityDto>();
        activity.Appointments.Total.ShouldBe(1);
        activity.Appointments.PendingPayment.ShouldBe(1);
        activity.Audit.ShouldContain(a => a.EntityType == "appointments" && a.Action == "created");
        activity.Audit.ShouldContain(a => a.EntityType == "payments" && a.Action == "created");
        activity.Audit.ShouldAllBe(a => a.ActorId == patient.UserId);

        var doctorActivity = await (await admin.GetAsync($"/api/v1/admin/users/{doctor.UserId}/activity", Ct)).ReadAsync<UserActivityDto>();
        doctorActivity.Appointments.Total.ShouldBe(1);
        (await admin.GetAsync($"/api/v1/admin/users/{Guid.NewGuid()}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task User_activity_records_login_logout_and_profile_updates()
    {
        var auth = await Factory.SignInWithPhoneAsync("+94770004444");
        var client = Factory.CreateClient().WithBearer(auth.AccessToken);
        var me = await (await client.GetAsync("/api/v1/me", Ct)).ReadAsync<MeDto>();
        (await client.PutJsonAsync("/api/v1/me", new
        {
            fullName = "Lasana Pahanga",
            language = "en",
            version = me.Version,
        })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Factory.CreateClient().PostJsonAsync("/api/v1/auth/logout", new { refreshToken = auth.RefreshToken }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var admin = await Factory.AdminClientAsync(AdminRole.Ops);
        var activity = await (await admin.GetAsync($"/api/v1/admin/users/{auth.User.Id}/activity", Ct)).ReadAsync<UserActivityDto>();
        activity.Audit.ShouldContain(a => a.EntityType == "sessions" && a.Action == "logged_in");
        activity.Audit.ShouldContain(a => a.EntityType == "sessions" && a.Action == "logged_out");
        activity.Audit.ShouldContain(a => a.EntityType == "users" && a.Action == "updated");
    }

    [Fact]
    public async Task Suspending_a_patient_cancels_their_upcoming_live_bookings_with_a_full_refund()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var confirmed = await Factory.CapturedAsync(patient, doctor.DoctorId, 9);
        var unpaid = await patient.Client.BookedAsync(doctor.DoctorId, BookingFlows.LocalTime(Factory.Today().AddDays(8), 10));
        var test = await Factory.CapturedAsync(patient, doctor.DoctorId, 11);
        await Fixture.MarkTestAsync(test.Id);
        var admin = await Factory.AdminClientAsync(AdminRole.Support);

        (await admin.PostJsonAsync($"/api/v1/admin/users/{patient.UserId}/suspend", new { reason = "Fraud" })).StatusCode.ShouldBe(HttpStatusCode.OK);

        string Row(Guid id) => $"SELECT concat_ws('|', status, cancelled_by, cancellation_reason, refund_percent) FROM appointments WHERE id = '{id}'";
        (await Fixture.ScalarAsync<string>(Row(confirmed.Id))).ShouldBe("cancelled|admin|patient_suspended|100");
        (await Fixture.ScalarAsync<string>(Row(unpaid.Id))).ShouldBe("cancelled|admin|patient_suspended");
        (await Fixture.ScalarAsync<string>(Row(test.Id))).ShouldBe("confirmed");
        (await Fixture.ScalarAsync<string>($"SELECT concat_ws('|', status, reason, amount_cents) FROM refunds WHERE payment_id = '{await Fixture.PaymentIdAsync(confirmed.Id)}'"))
            .ShouldBe($"processing|admin_cancellation|{Fee}");
        (await Fixture.ScalarAsync<string>($"SELECT status FROM payments WHERE appointment_id = '{unpaid.Id}'")).ShouldBe("failed");
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM notifications WHERE template_key = 'appointment_cancelled_doctor' AND user_id = '{doctor.UserId}'"))
            .ShouldBeGreaterThan(0);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM notifications WHERE dedupe_key LIKE 'appt:{unpaid.Id}:cancelled:doctor%'")).ShouldBe(0);
    }

    [Fact]
    public async Task Suspending_a_doctor_user_hides_the_profile_and_reinstating_lifts_only_that_suspension()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var booked = await Factory.CapturedAsync(patient, doctor.DoctorId, 9);
        var admin = await Factory.AdminClientAsync(AdminRole.SuperAdmin);
        var publicClient = Factory.CreateClient();

        (await admin.PostJsonAsync($"/api/v1/admin/users/{doctor.UserId}/suspend", new { reason = "Licence check" })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var suspended = await (await admin.GetAsync($"/api/v1/admin/doctors/{doctor.DoctorId}", Ct)).ReadAsync<AdminDoctorDto>();
        suspended.Status.ShouldBe(DoctorStatus.Suspended);
        suspended.SuspendedWithUser.ShouldBeTrue();
        suspended.SuspendedReason.ShouldBe("Licence check");
        (await publicClient.GetAsync($"/api/v1/doctors/{doctor.DoctorId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await patient.Client.BookAsync(doctor.DoctorId, BookingFlows.LocalTime(Factory.Today().AddDays(8), 12))).StatusCode.ShouldNotBe(HttpStatusCode.Created);
        (await patient.Client.AppointmentAsync(booked.Id)).Status.ShouldBe(AppointmentStatus.Confirmed);

        (await admin.PostAsync($"/api/v1/admin/users/{doctor.UserId}/reinstate", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var reinstated = await (await admin.GetAsync($"/api/v1/admin/doctors/{doctor.DoctorId}", Ct)).ReadAsync<AdminDoctorDto>();
        reinstated.Status.ShouldBe(DoctorStatus.Active);
        reinstated.SuspendedWithUser.ShouldBeFalse();
        (await publicClient.GetAsync($"/api/v1/doctors/{doctor.DoctorId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctor.DoctorId}/suspend", new { reason = "Complaint" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PostJsonAsync($"/api/v1/admin/users/{doctor.UserId}/suspend", new { reason = "Licence check" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/v1/admin/users/{doctor.UserId}/reinstate", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var stillSuspended = await (await admin.GetAsync($"/api/v1/admin/doctors/{doctor.DoctorId}", Ct)).ReadAsync<AdminDoctorDto>();
        stillSuspended.Status.ShouldBe(DoctorStatus.Suspended);
        stillSuspended.SuspendedReason.ShouldBe("Complaint");
    }

    [Fact]
    public async Task Admin_can_reset_user_password_and_revoke_active_sessions()
    {
        var auth = await Factory.CreateUserAsync(UserRole.Patient, "resetme@example.com", "old password 123");
        var patient = Factory.CreateClient().WithBearer(auth.AccessToken);
        var admin = await Factory.AdminClientAsync(AdminRole.Admin);

        (await patient.GetAsync("/api/v1/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await admin.PostJsonAsync($"/api/v1/admin/users/{auth.User.Id}/reset-password", new { newPassword = "new password 456" });
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await patient.GetAsync("/api/v1/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var oldLogin = await Factory.CreateClient().PostJsonAsync("/api/v1/auth/login/email", new { email = "resetme@example.com", password = "old password 123" });
        oldLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var newLogin = await (await Factory.CreateClient().PostJsonAsync("/api/v1/auth/login/email", new { email = "resetme@example.com", password = "new password 456" }))
            .ReadAsync<AuthResponse>();
        newLogin.AccessToken.ShouldNotBeNullOrWhiteSpace();

        var freshClient = Factory.CreateClient().WithBearer(newLogin.AccessToken);
        (await freshClient.GetAsync("/api/v1/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
