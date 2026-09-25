using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.FinanceFlows;

namespace TeleMed.Api.IntegrationTests;

public class AdminRefundTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Admin, Guid PaymentId)> CapturedPaymentAsync()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var appointment = await Factory.CapturedAsync(patient, doctor.DoctorId, 9);
        return (await Factory.AdminClientAsync(AdminRole.Finance), await Fixture.PaymentIdAsync(appointment.Id));
    }

    private static async Task<AdminRefundDto> RequestAsync(HttpClient admin, Guid paymentId, long amountCents) =>
        await (await admin.PostJsonAsync($"/api/v1/admin/payments/{paymentId}/refunds", new { amountCents, reason = "Goodwill" }))
            .ReadAsync<AdminRefundDto>(HttpStatusCode.Created);

    [Fact]
    public async Task Manual_refunds_are_bounded_by_what_is_left_and_approved_ones_settle_through_the_job()
    {
        var (admin, paymentId) = await CapturedPaymentAsync();

        var tooMuch = await admin.PostJsonAsync($"/api/v1/admin/payments/{paymentId}/refunds", new { amountCents = Fee + 1, reason = "Goodwill" });
        tooMuch.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await tooMuch.ProblemCodeAsync()).ShouldBe("refund_exceeds_refundable");

        var first = await RequestAsync(admin, paymentId, 100_000);
        first.Status.ShouldBe(RefundStatus.Requested);
        first.Reason.ShouldBe(RefundReason.AdminRequest);
        first.Note.ShouldBe("Goodwill");
        (first.CommissionCents + first.ProviderFeeCents + first.PayoutCents).ShouldBe(100_000);

        var overReserved = await admin.PostJsonAsync($"/api/v1/admin/payments/{paymentId}/refunds", new { amountCents = 150_001, reason = "Again" });
        overReserved.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var approved = await (await admin.PostAsync($"/api/v1/admin/finance/refunds/{first.Id}/approve", null, Ct)).ReadAsync<AdminRefundDto>();
        approved.Status.ShouldBe(RefundStatus.Processing);
        approved.ReviewedByAdminId.ShouldNotBeNull();
        (await admin.PostAsync($"/api/v1/admin/finance/refunds/{first.Id}/approve", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        await Factory.SettleAsync();
        (await Fixture.ScalarAsync<string>($"SELECT status || ':' || refunded_cents FROM payments WHERE id = '{paymentId}'")).ShouldBe("partially_refunded:100000");

        var second = await RequestAsync(admin, paymentId, 150_000);
        var rejected = await (await admin.PostJsonAsync($"/api/v1/admin/finance/refunds/{second.Id}/reject", new { reason = "Not eligible" }))
            .ReadAsync<AdminRefundDto>();
        rejected.Status.ShouldBe(RefundStatus.Rejected);
        rejected.RejectionReason.ShouldBe("Not eligible");

        // A rejected request no longer holds any of the balance.
        (await RequestAsync(admin, paymentId, 150_000)).Status.ShouldBe(RefundStatus.Requested);

        var requested = await (await admin.GetAsync("/api/v1/admin/finance/refunds?status=requested", Ct)).ReadAsync<PagedResult<AdminRefundDto>>();
        requested.Items.ShouldHaveSingleItem().AmountCents.ShouldBe(150_000);
    }

    [Fact]
    public async Task Manual_required_refunds_are_marked_refunded_with_the_portal_reference()
    {
        var (admin, paymentId) = await CapturedPaymentAsync();
        var refund = await RequestAsync(admin, paymentId, Fee);
        (await admin.PostJsonAsync($"/api/v1/admin/finance/refunds/{refund.Id}/mark-refunded", new { reference = "" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync($"/api/v1/admin/finance/refunds/{refund.Id}/mark-refunded", new { reference = "PH-1" })).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);
        await Fixture.ExecuteSqlAsync($"UPDATE refunds SET status = 'manual_required' WHERE id = '{refund.Id}'");

        var done = await (await admin.PostJsonAsync($"/api/v1/admin/finance/refunds/{refund.Id}/mark-refunded", new { reference = "PH-REF-9912" }))
            .ReadAsync<AdminRefundDto>();

        done.Status.ShouldBe(RefundStatus.Succeeded);
        done.ProviderRefundId.ShouldBe("PH-REF-9912");
        done.ProcessedAt.ShouldNotBeNull();
        (await Fixture.ScalarAsync<string>($"SELECT status || ':' || refunded_cents || ':' || refunded_payout_cents FROM payments WHERE id = '{paymentId}'"))
            .ShouldBe($"refunded:{Fee}:{DoctorShare}");
    }

    [Fact]
    public async Task Only_captured_payments_can_be_refunded_by_hand()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var booked = await patient.Client.BookedAsync(doctor.DoctorId, BookingFlows.LocalTime(Factory.Today().AddDays(8), 9));
        var admin = await Factory.AdminClientAsync(AdminRole.Finance);

        var response = await admin.PostJsonAsync($"/api/v1/admin/payments/{await Fixture.PaymentIdAsync(booked.Id)}/refunds", new { amountCents = 100, reason = "x" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe("payment_not_refundable");
        (await admin.PostJsonAsync($"/api/v1/admin/payments/{Guid.NewGuid()}/refunds", new { amountCents = 100, reason = "x" })).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }
}
