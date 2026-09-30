using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.AdminUsers;
using TeleMed.Application.Admin.Content;
using TeleMed.Application.Admin.Disputes;
using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Doctors;
using TeleMed.Application.Permissions;
using TeleMed.Application.Scheduling;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests;

public class AdminPermissionMatrixTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static readonly AdminRole[] Everyone = Enum.GetValues<AdminRole>();

    private static readonly Dictionary<AdminPermission, AdminRole[]> ExpectedMatrix = new()
    {
        [AdminPermission.Credentialing] = Everyone,
        [AdminPermission.Doctors] = Everyone,
        [AdminPermission.Users] = Everyone,
        [AdminPermission.Appointments] = Everyone,
        [AdminPermission.Content] = Everyone,
        [AdminPermission.Disputes] = Everyone,
        [AdminPermission.Analytics] = Everyone,
        [AdminPermission.Audit] = Everyone,
        [AdminPermission.Finance] = [AdminRole.Finance, AdminRole.SuperAdmin],
        [AdminPermission.AuditExport] = [AdminRole.Finance, AdminRole.SuperAdmin],
        [AdminPermission.AdminUsers] = [AdminRole.SuperAdmin],
    };

    private sealed record Target(
        Guid AdminId,
        string SpecialtyCode,
        Guid DrugId,
        Guid ReviewedApplicationId,
        Guid ReviewedDocumentId,
        Guid ApprovableApplicationId,
        Guid RejectableApplicationId,
        Guid DoctorId,
        Guid DoctorDocumentId,
        Guid HolidayId,
        Guid SlotBlockId,
        Guid AppointmentId,
        Guid AcceptableRescheduleId,
        Guid DeclinableRescheduleId,
        Guid AdminNotificationId,
        Guid PayoutBatchId,
        Guid PaidPayoutId,
        Guid FailedPayoutId,
        Guid CapturedPaymentId,
        Guid ApprovableRefundId,
        Guid RejectableRefundId,
        Guid ManualRefundId,
        Guid PromoCodeId,
        Guid DisputeId,
        Guid UserId);

    private sealed record Route(string Method, string Template, AdminPermission? Permission, Func<Target, string> Url, object? Body = null);

    private static readonly Route[] Routes =
    [
        new("GET", "admin/me", null, _ => "admin/me"),
        new("GET", "admin/permissions", null, _ => "admin/permissions"),
        new("GET", "admin/admin-users", AdminPermission.AdminUsers, _ => "admin/admin-users"),
        new("POST", "admin/admin-users", AdminPermission.AdminUsers, _ => "admin/admin-users",
            new { email = $"new-{Guid.NewGuid():N}@admin.test", displayName = "New", role = "support" }),
        new("PATCH", "admin/admin-users/{id:guid}", AdminPermission.AdminUsers, t => $"admin/admin-users/{t.AdminId}", new { displayName = "Renamed" }),
        new("POST", "admin/admin-users/{id:guid}/deactivate", AdminPermission.AdminUsers, t => $"admin/admin-users/{t.AdminId}/deactivate"),
        new("GET", "admin/specialties", AdminPermission.Content, _ => "admin/specialties"),
        new("POST", "admin/specialties", AdminPermission.Content, _ => "admin/specialties",
            new { code = "matrix_new", nameEn = "New", nameSi = "නව", nameTa = "புதிய", displayOrder = 500 }),
        new("GET", "admin/specialties/{code}", AdminPermission.Content, t => $"admin/specialties/{t.SpecialtyCode}"),
        new("PUT", "admin/specialties/{code}", AdminPermission.Content, t => $"admin/specialties/{t.SpecialtyCode}",
            new { nameEn = "Renamed", nameSi = "නව", nameTa = "புதிய", displayOrder = 1 }),
        new("DELETE", "admin/specialties/{code}", AdminPermission.Content, t => $"admin/specialties/{t.SpecialtyCode}"),
        new("GET", "admin/drugs", AdminPermission.Content, _ => "admin/drugs"),
        new("POST", "admin/drugs", AdminPermission.Content, _ => "admin/drugs",
            new { name = "Matrixol", genericName = "Matrixamine", strength = "5mg", form = "tablet", isControlled = false, isGeneric = true }),
        new("GET", "admin/drugs/{id:guid}", AdminPermission.Content, t => $"admin/drugs/{t.DrugId}"),
        new("PUT", "admin/drugs/{id:guid}", AdminPermission.Content, t => $"admin/drugs/{t.DrugId}",
            new { name = "Matrixol", genericName = "Matrixamine", strength = "10mg", form = "tablet", isControlled = false, isGeneric = true }),
        new("DELETE", "admin/drugs/{id:guid}", AdminPermission.Content, t => $"admin/drugs/{t.DrugId}"),
        new("GET", "admin/doctor-applications", AdminPermission.Credentialing, _ => "admin/doctor-applications"),
        new("GET", "admin/doctor-applications/{id:guid}", AdminPermission.Credentialing, t => $"admin/doctor-applications/{t.ReviewedApplicationId}"),
        new("GET", "admin/doctor-applications/{id:guid}/documents/{documentId:guid}", AdminPermission.Credentialing,
            t => $"admin/doctor-applications/{t.ReviewedApplicationId}/documents/{t.ReviewedDocumentId}"),
        new("PUT", "admin/doctor-applications/{id:guid}/checklist", AdminPermission.Credentialing,
            t => $"admin/doctor-applications/{t.ReviewedApplicationId}/checklist", new { slmcFormat = true }),
        new("POST", "admin/doctor-applications/{id:guid}/start-review", AdminPermission.Credentialing,
            t => $"admin/doctor-applications/{t.ReviewedApplicationId}/start-review"),
        new("POST", "admin/doctor-applications/{id:guid}/approve", AdminPermission.Credentialing,
            t => $"admin/doctor-applications/{t.ApprovableApplicationId}/approve"),
        new("POST", "admin/doctor-applications/{id:guid}/reject", AdminPermission.Credentialing,
            t => $"admin/doctor-applications/{t.RejectableApplicationId}/reject", new { reason = "Incomplete" }),
        new("GET", "admin/doctors", AdminPermission.Doctors, _ => "admin/doctors"),
        new("GET", "admin/doctors/{id:guid}", AdminPermission.Doctors, t => $"admin/doctors/{t.DoctorId}"),
        new("GET", "admin/doctors/{id:guid}/documents/{documentId:guid}", AdminPermission.Doctors,
            t => $"admin/doctors/{t.DoctorId}/documents/{t.DoctorDocumentId}"),
        new("POST", "admin/doctors/{id:guid}/suspend", AdminPermission.Doctors, t => $"admin/doctors/{t.DoctorId}/suspend", new { reason = "Review" }),
        new("POST", "admin/doctors/{id:guid}/reinstate", AdminPermission.Doctors, t => $"admin/doctors/{t.DoctorId}/reinstate"),
        new("GET", "admin/doctors/{id:guid}/schedule", AdminPermission.Doctors, t => $"admin/doctors/{t.DoctorId}/schedule"),
        new("PUT", "admin/doctors/{id:guid}/schedule", AdminPermission.Doctors, t => $"admin/doctors/{t.DoctorId}/schedule",
            new { slotDurationMinutes = 20, bufferMinutes = 5, maxPerDay = 10, advanceDays = 14, timezone = "Asia/Colombo", workingHours = new[] { new { dayOfWeek = 1, startMinute = 540, endMinute = 720 } } }),
        new("GET", "admin/doctors/{id:guid}/holidays", AdminPermission.Doctors, t => $"admin/doctors/{t.DoctorId}/holidays"),
        new("POST", "admin/doctors/{id:guid}/holidays", AdminPermission.Doctors, t => $"admin/doctors/{t.DoctorId}/holidays",
            new { date = "2030-02-01", reason = "Leave" }),
        new("GET", "admin/doctors/{id:guid}/slot-blocks", AdminPermission.Doctors, t => $"admin/doctors/{t.DoctorId}/slot-blocks"),
        new("POST", "admin/doctors/{id:guid}/slot-blocks", AdminPermission.Doctors, t => $"admin/doctors/{t.DoctorId}/slot-blocks",
            new { startAt = "2030-02-02T04:00:00Z", endAt = "2030-02-02T05:00:00Z", reason = "Maintenance" }),
        new("DELETE", "admin/slot-blocks/{id:guid}", AdminPermission.Doctors, t => $"admin/slot-blocks/{t.SlotBlockId}"),
        new("GET", "admin/holidays", AdminPermission.Doctors, _ => "admin/holidays"),
        new("POST", "admin/holidays", AdminPermission.Doctors, _ => "admin/holidays", new { date = "2030-02-03", reason = "Poya" }),
        new("DELETE", "admin/holidays/{id:guid}", AdminPermission.Doctors, t => $"admin/holidays/{t.HolidayId}"),
        new("GET", "admin/appointments", AdminPermission.Appointments, _ => "admin/appointments"),
        new("GET", "admin/appointments/{id:guid}", AdminPermission.Appointments, t => $"admin/appointments/{t.AppointmentId}"),
        new("GET", "admin/appointments/{id:guid}/audit", AdminPermission.Appointments, t => $"admin/appointments/{t.AppointmentId}/audit"),
        new("POST", "admin/appointments/{id:guid}/cancel", AdminPermission.Appointments, t => $"admin/appointments/{t.AppointmentId}/cancel",
            new { reason = "Matrix" }),
        new("GET", "admin/reschedule-requests", AdminPermission.Appointments, _ => "admin/reschedule-requests"),
        new("POST", "admin/reschedule-requests/{id:guid}/accept", AdminPermission.Appointments,
            t => $"admin/reschedule-requests/{t.AcceptableRescheduleId}/accept"),
        new("POST", "admin/reschedule-requests/{id:guid}/decline", AdminPermission.Appointments,
            t => $"admin/reschedule-requests/{t.DeclinableRescheduleId}/decline"),
        new("GET", "admin/notifications", null, _ => "admin/notifications?unreadOnly=true"),
        new("GET", "admin/notifications/unread-count", null, _ => "admin/notifications/unread-count"),
        new("POST", "admin/notifications/{id:guid}/read", null, t => $"admin/notifications/{t.AdminNotificationId}/read"),
        new("POST", "admin/notifications/read-all", null, _ => "admin/notifications/read-all"),
        new("GET", "admin/finance/ledger", AdminPermission.Finance, _ => "admin/finance/ledger"),
        new("GET", "admin/finance/ledger.csv", AdminPermission.Finance, _ => "admin/finance/ledger.csv"),
        new("GET", "admin/finance/commission", AdminPermission.Finance, _ => "admin/finance/commission"),
        new("GET", "admin/finance/payout-batches", AdminPermission.Finance, _ => "admin/finance/payout-batches"),
        new("GET", "admin/finance/payout-batches/{id:guid}", AdminPermission.Finance, t => $"admin/finance/payout-batches/{t.PayoutBatchId}"),
        new("POST", "admin/finance/payouts/run", AdminPermission.Finance, _ => "admin/finance/payouts/run", new { }),
        new("POST", "admin/finance/payouts/{id:guid}/mark-paid", AdminPermission.Finance,
            t => $"admin/finance/payouts/{t.PaidPayoutId}/mark-paid", new { transferReference = "BOC-1" }),
        new("POST", "admin/finance/payouts/{id:guid}/mark-failed", AdminPermission.Finance,
            t => $"admin/finance/payouts/{t.FailedPayoutId}/mark-failed", new { reason = "Closed account" }),
        new("GET", "admin/finance/refunds", AdminPermission.Finance, _ => "admin/finance/refunds"),
        new("POST", "admin/finance/refunds/{id:guid}/approve", AdminPermission.Finance, t => $"admin/finance/refunds/{t.ApprovableRefundId}/approve"),
        new("POST", "admin/finance/refunds/{id:guid}/reject", AdminPermission.Finance,
            t => $"admin/finance/refunds/{t.RejectableRefundId}/reject", new { reason = "No" }),
        new("POST", "admin/finance/refunds/{id:guid}/mark-refunded", AdminPermission.Finance,
            t => $"admin/finance/refunds/{t.ManualRefundId}/mark-refunded", new { reference = "PH-1" }),
        new("POST", "admin/payments/{id:guid}/refunds", AdminPermission.Finance,
            t => $"admin/payments/{t.CapturedPaymentId}/refunds", new { amountCents = 1_000, reason = "Goodwill" }),
        new("GET", "admin/finance/promo-codes", AdminPermission.Finance, _ => "admin/finance/promo-codes"),
        new("POST", "admin/finance/promo-codes", AdminPermission.Finance, _ => "admin/finance/promo-codes",
            new { code = "MATRIX2", discountType = "fixed", amountOffCents = 1_000, minAmountCents = 0 }),
        new("PATCH", "admin/finance/promo-codes/{id:guid}", AdminPermission.Finance,
            t => $"admin/finance/promo-codes/{t.PromoCodeId}", new { description = "Changed" }),
        new("POST", "admin/finance/promo-codes/{id:guid}/deactivate", AdminPermission.Finance, t => $"admin/finance/promo-codes/{t.PromoCodeId}/deactivate"),
        new("GET", "admin/disputes", AdminPermission.Disputes, _ => "admin/disputes"),
        new("POST", "admin/disputes", AdminPermission.Disputes, _ => "admin/disputes",
            new { appointmentId = Guid.Empty, subject = "placeholder", description = "replaced at run time" }),
        new("GET", "admin/disputes/{id:guid}", AdminPermission.Disputes, t => $"admin/disputes/{t.DisputeId}"),
        new("POST", "admin/disputes/{id:guid}/comments", AdminPermission.Disputes, t => $"admin/disputes/{t.DisputeId}/comments", new { body = "Noted" }),
        new("POST", "admin/disputes/{id:guid}/assign", AdminPermission.Disputes, t => $"admin/disputes/{t.DisputeId}/assign", new { }),
        new("POST", "admin/disputes/{id:guid}/resolve", AdminPermission.Disputes, t => $"admin/disputes/{t.DisputeId}/resolve", new { resolution = "Done" }),
        new("POST", "admin/disputes/{id:guid}/close", AdminPermission.Disputes, t => $"admin/disputes/{t.DisputeId}/close"),
        new("GET", "admin/users", AdminPermission.Users, _ => "admin/users"),
        new("GET", "admin/users/{id:guid}", AdminPermission.Users, t => $"admin/users/{t.UserId}"),
        new("GET", "admin/users/{id:guid}/activity", AdminPermission.Users, t => $"admin/users/{t.UserId}/activity"),
        new("POST", "admin/users/{id:guid}/suspend", AdminPermission.Users, t => $"admin/users/{t.UserId}/suspend", new { reason = "Matrix" }),
        new("POST", "admin/users/{id:guid}/reinstate", AdminPermission.Users, t => $"admin/users/{t.UserId}/reinstate"),
        new("POST", "admin/users/{id:guid}/reset-password", AdminPermission.Users, t => $"admin/users/{t.UserId}/reset-password", new { newPassword = "newPassword123!" }),
        new("GET", "admin/audit", AdminPermission.Audit, _ => "admin/audit"),
        new("GET", "admin/audit.csv", AdminPermission.AuditExport, _ => "admin/audit.csv"),
        new("GET", "admin/analytics/dashboard", AdminPermission.Analytics, _ => "admin/analytics/dashboard"),
        new("GET", "admin/analytics/revenue", AdminPermission.Analytics, _ => "admin/analytics/revenue?granularity=week"),
        new("GET", "admin/analytics/bookings", AdminPermission.Analytics, _ => "admin/analytics/bookings"),
        new("GET", "admin/analytics/top-doctors", AdminPermission.Analytics, _ => "admin/analytics/top-doctors"),
    ];

    [Theory]
    [InlineData(AdminRole.SuperAdmin)]
    [InlineData(AdminRole.Admin)]
    [InlineData(AdminRole.Ops)]
    [InlineData(AdminRole.Finance)]
    [InlineData(AdminRole.Support)]
    public async Task Every_admin_route_enforces_the_matrix(AdminRole role)
    {
        var client = await Factory.AdminClientAsync(role);
        var target = await SeedTargetAsync(await Factory.AdminClientAsync(AdminRole.SuperAdmin));

        foreach (var route in Routes)
        {
            var request = new HttpRequestMessage(new HttpMethod(route.Method), "/api/v1/" + route.Url(target));
            var body = route.Template == "admin/disputes" && route.Method == "POST"
                ? new { appointmentId = target.AppointmentId, subject = "Matrix", description = "Opened by the matrix" }
                : route.Body;
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: TeleMed.Api.IntegrationTests.Infrastructure.Api.Json);
            }

            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            var allowed = route.Permission is not { } permission || ExpectedMatrix[permission].Contains(role);
            var description = $"{role} {route.Method} {route.Template}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}";
            if (allowed)
            {
                ((int)response.StatusCode).ShouldBeInRange(200, 299, description);
            }
            else
            {
                response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, description);
            }
        }
    }

    [Fact]
    public void Matrix_covers_every_admin_endpoint()
    {
        var mapped = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("api/v1/admin", StringComparison.Ordinal))
            .SelectMany(e => e.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods
                .Select(m => $"{m} {e.RoutePattern.RawText!["api/v1/".Length..]}"))
            .Order()
            .ToList();

        mapped.ShouldBe(Routes.Select(r => $"{r.Method} {r.Template}").Order().ToList());
    }

    [Fact]
    public async Task Permissions_endpoint_publishes_the_matrix_and_me_lists_own_permissions()
    {
        var client = await Factory.AdminClientAsync(AdminRole.Finance);

        var matrix = await (await client.GetAsync("/api/v1/admin/permissions", TestContext.Current.CancellationToken))
            .ReadAsync<List<PermissionRolesDto>>();
        var me = await (await client.GetAsync("/api/v1/admin/me", TestContext.Current.CancellationToken)).ReadAsync<AdminMeDto>();

        matrix.Select(p => $"{p.Permission}: {string.Join(",", p.Roles.Order())}").Order()
            .ShouldBe(ExpectedMatrix.Select(p => $"{p.Key}: {string.Join(",", p.Value.Order())}").Order());
        me.Role.ShouldBe(AdminRole.Finance);
        me.Permissions.ShouldContain(AdminPermission.Finance);
        me.Permissions.ShouldContain(AdminPermission.AuditExport);
        me.Permissions.ShouldNotContain(AdminPermission.AdminUsers);
    }

    private async Task<Target> SeedTargetAsync(HttpClient superAdmin)
    {
        var admin = await Factory.CreateAdminAsync(AdminRole.Support);
        var specialty = await superAdmin.PostJsonAsync("/api/v1/admin/specialties",
            new { code = "matrix_target", nameEn = "Target", nameSi = "ඉලක්ක", nameTa = "இலக்கு", displayOrder = 900 });
        var drug = await (await superAdmin.PostJsonAsync("/api/v1/admin/drugs",
                new { name = "Targetol", genericName = "Targetamine", strength = "1mg", form = "tablet", isControlled = false, isGeneric = true }))
            .ReadAsync<AdminDrugDto>(HttpStatusCode.Created);
        specialty.StatusCode.ShouldBe(HttpStatusCode.Created);

        var reviewed = await Factory.SubmitApplicationAsync(new DoctorFlows.ApplicationSpec { Phone = "+94770000001", Email = "r@example.com", SlmcNumber = "10001" });
        var reviewedDocument = await (await Factory.UploadApplicationDocumentAsync(reviewed.Id, "nic", reviewed.UploadToken, DoctorFlows.Pdf))
            .ReadAsync<DoctorDocumentDto>();
        var approvable = await Factory.SubmitApplicationAsync(new DoctorFlows.ApplicationSpec { Phone = "+94770000002", Email = "a@example.com", SlmcNumber = "10002" });
        var rejectable = await Factory.SubmitApplicationAsync(new DoctorFlows.ApplicationSpec { Phone = "+94770000003", Email = "x@example.com", SlmcNumber = "10003" });
        var existing = await Factory.SubmitApplicationAsync(new DoctorFlows.ApplicationSpec { Phone = "+94770000004", Email = "d@example.com", SlmcNumber = "10004" });
        var doctorDocument = await (await Factory.UploadApplicationDocumentAsync(existing.Id, "slmcCertificate", existing.UploadToken, DoctorFlows.Pdf))
            .ReadAsync<DoctorDocumentDto>();
        var doctor = await superAdmin.ApproveAsync(existing.Id);
        var doctorId = doctor.DoctorId!.Value;
        var holiday = await (await superAdmin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/holidays", new { date = "2030-01-01", reason = "Leave" }))
            .ReadAsync<HolidayDto>(HttpStatusCode.Created);
        var block = await (await superAdmin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/slot-blocks",
                new { startAt = "2030-01-02T04:00:00Z", endAt = "2030-01-02T05:00:00Z", reason = "Maintenance" }))
            .ReadAsync<SlotBlockDto>(HttpStatusCode.Created);

        var bookable = await Factory.BookableDoctorAsync(new DoctorFlows.ApplicationSpec { Phone = "+94770000005", Email = "b@example.com", SlmcNumber = "10005" });
        var patient = await Factory.PatientAsync();
        var day = Factory.Today().AddDays(2);
        var appointment = await patient.PaidAsync(bookable.DoctorId, BookingFlows.LocalTime(day, 9));
        var acceptable = await patient.PaidAsync(bookable.DoctorId, BookingFlows.LocalTime(day, 10));
        var declinable = await patient.PaidAsync(bookable.DoctorId, BookingFlows.LocalTime(day, 11));
        var accept = await bookable.Client.ProposedAsync(acceptable.Id, BookingFlows.LocalTime(day, 14));
        var decline = await bookable.Client.ProposedAsync(declinable.Id, BookingFlows.LocalTime(day, 15));

        var adminNotificationId = await Fixture.ScalarAsync<Guid>("SELECT id FROM admin_notifications ORDER BY created_at LIMIT 1");

        var captured = await Factory.CapturedAsync(patient, bookable.DoctorId, 9);
        var paymentId = await Fixture.PaymentIdAsync(captured.Id);
        async Task<Guid> RefundAsync() =>
            (await (await superAdmin.PostJsonAsync($"/api/v1/admin/payments/{paymentId}/refunds", new { amountCents = 10_000, reason = "Matrix" }))
                .ReadAsync<AdminRefundDto>(HttpStatusCode.Created)).Id;
        var approvableRefund = await RefundAsync();
        var rejectableRefund = await RefundAsync();
        var manualRefund = await RefundAsync();
        await Fixture.ExecuteSqlAsync($"UPDATE refunds SET status = 'manual_required' WHERE id = '{manualRefund}'");

        var (batchId, paidPayoutId, failedPayoutId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await Fixture.ExecuteSqlAsync($"""
            INSERT INTO payout_batches (id, period_start, period_end, status, created_at, updated_at)
            VALUES ('{batchId}', '2030-01-01', '2030-01-01', 'pending', now(), now());
            INSERT INTO payouts (id, batch_id, doctor_id, period, amount_cents, payment_count, currency, status, created_at, updated_at)
            VALUES ('{paidPayoutId}', '{batchId}', '{doctorId}', '2030-01-01', 1000, 1, 'LKR', 'pending', now(), now()),
                   ('{failedPayoutId}', '{batchId}', '{bookable.DoctorId}', '2030-01-01', 1000, 1, 'LKR', 'pending', now(), now());
            """);

        var promo = await (await superAdmin.PostJsonAsync("/api/v1/admin/finance/promo-codes",
                new { code = "MATRIX1", discountType = "percent", percentBps = 500, minAmountCents = 0 }))
            .ReadAsync<PromoCodeDto>(HttpStatusCode.Created);
        var dispute = await (await superAdmin.PostJsonAsync("/api/v1/admin/disputes",
                new { appointmentId = appointment.Id, subject = "Matrix", description = "Seeded" }))
            .ReadAsync<DisputeDetailDto>(HttpStatusCode.Created);
        var user = await Factory.CreateUserAsync(UserRole.Patient);

        return new Target(
            admin.Id, "matrix_target", drug.Id, reviewed.Id, reviewedDocument.Id, approvable.Id, rejectable.Id, doctorId, doctorDocument.Id, holiday.Id, block.Id,
            appointment.Id, accept.Id, decline.Id, adminNotificationId, batchId, paidPayoutId, failedPayoutId, paymentId, approvableRefund, rejectableRefund,
            manualRefund, promo.Id, dispute.Dispute.Id, user.User.Id);
    }
}
