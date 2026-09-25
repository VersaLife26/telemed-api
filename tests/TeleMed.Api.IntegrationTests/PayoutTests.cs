using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Analytics;
using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Common;
using TeleMed.Application.Jobs;
using TeleMed.Application.Payouts;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.FinanceFlows;

namespace TeleMed.Api.IntegrationTests;

public class PayoutTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<long> CountAsync(string table) => Fixture.ScalarAsync<long>($"SELECT count(*) FROM {table}");

    [Fact]
    public async Task Daily_job_pays_held_back_payments_once_and_skips_test_appointments_and_payments_still_on_hold()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var start = Factory.Time.GetUtcNow();
        var paid = await Fixture.CompletedAsync(patient, doctor.DoctorId, 9);
        var test = await Fixture.CompletedAsync(patient, doctor.DoctorId, 10);
        await Fixture.MarkTestAsync(test.Id);

        var runAt = LocalHourAtOrAfter(start + TimeSpan.FromHours(25), 3);
        Factory.AdvanceTo(runAt - TimeSpan.FromHours(23));
        var onHold = await Fixture.CompletedAsync(patient, doctor.DoctorId, 11);
        Factory.AdvanceTo(runAt);

        await Factory.RunJobAsync<DailyPayoutsJob>();

        var period = LocalDate(runAt).AddDays(-1);
        var admin = await Factory.AdminClientAsync(AdminRole.Finance);
        var batches = await (await admin.GetAsync("/api/v1/admin/finance/payout-batches", Ct)).ReadAsync<PagedResult<PayoutBatchDto>>();
        var batch = batches.Items.ShouldHaveSingleItem();
        batch.PeriodEnd.ShouldBe(period);
        batch.Status.ShouldBe(PayoutBatchStatus.Pending);
        var detail = await (await admin.GetAsync($"/api/v1/admin/finance/payout-batches/{batch.Id}", Ct)).ReadAsync<PayoutBatchDetailDto>();
        var payout = detail.Payouts.ShouldHaveSingleItem();
        payout.DoctorId.ShouldBe(doctor.DoctorId);
        payout.AmountCents.ShouldBe(DoctorShare);
        payout.PaymentCount.ShouldBe(1);
        (await Fixture.ScalarAsync<Guid?>($"SELECT payout_id FROM payments WHERE appointment_id = '{paid.Id}'")).ShouldBe(payout.Id);
        (await Fixture.ScalarAsync<Guid?>($"SELECT payout_id FROM payments WHERE appointment_id = '{test.Id}'")).ShouldBeNull();
        (await Fixture.ScalarAsync<Guid?>($"SELECT payout_id FROM payments WHERE appointment_id = '{onHold.Id}'")).ShouldBeNull();

        await Factory.RunJobAsync<DailyPayoutsJob>();
        var rerun = await (await admin.PostJsonAsync("/api/v1/admin/finance/payouts/run", new { date = period })).ReadAsync<PayoutRunDto>();
        rerun.Created.ShouldBeFalse();
        rerun.Batch!.Batch.Id.ShouldBe(batch.Id);
        (await CountAsync("payout_batches")).ShouldBe(1);
        (await CountAsync("payouts")).ShouldBe(1);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM payments WHERE payout_id IS NOT NULL")).ShouldBe(1);

        Factory.AdvanceTo(runAt + TimeSpan.FromDays(1));
        await Factory.RunJobAsync<DailyPayoutsJob>();
        (await Fixture.ScalarAsync<Guid?>($"SELECT payout_id FROM payments WHERE appointment_id = '{onHold.Id}'")).ShouldNotBeNull();
        (await Fixture.ScalarAsync<Guid?>($"SELECT payout_id FROM payments WHERE appointment_id = '{test.Id}'")).ShouldBeNull();
        (await CountAsync("payout_batches")).ShouldBe(2);
    }

    [Fact]
    public async Task A_run_for_a_day_that_has_not_ended_is_rejected_and_a_day_with_nothing_payable_builds_nothing()
    {
        var admin = await Factory.AdminClientAsync(AdminRole.SuperAdmin);
        var today = Factory.Today();

        var open = await admin.PostJsonAsync("/api/v1/admin/finance/payouts/run", new { date = today });
        open.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await open.ProblemCodeAsync()).ShouldBe("period_not_closed");

        var empty = await (await admin.PostAsync("/api/v1/admin/finance/payouts/run", null, Ct)).ReadAsync<PayoutRunDto>();
        empty.Created.ShouldBeFalse();
        empty.Batch.ShouldBeNull();
        empty.Period.ShouldBe(today.AddDays(-1));
    }

    [Fact]
    public async Task Failed_payouts_release_their_payments_to_the_next_run_and_paid_ones_show_in_the_doctors_earnings()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var appointment = await Fixture.CompletedAsync(patient, doctor.DoctorId, 9);
        var captureDate = Factory.Today();
        Factory.Time.Advance(TimeSpan.FromHours(49));

        var admin = await Factory.AdminClientAsync(AdminRole.Finance);
        var first = await (await admin.PostJsonAsync("/api/v1/admin/finance/payouts/run", new { date = Factory.Today().AddDays(-2) })).ReadAsync<PayoutRunDto>();
        first.Created.ShouldBeTrue();
        var failing = first.Batch!.Payouts.ShouldHaveSingleItem();

        var failed = await (await admin.PostJsonAsync($"/api/v1/admin/finance/payouts/{failing.Id}/mark-failed", new { reason = "Account closed" }))
            .ReadAsync<PayoutDto>();
        failed.Status.ShouldBe(PayoutStatus.Failed);
        failed.FailureReason.ShouldBe("Account closed");
        (await Fixture.ScalarAsync<Guid?>($"SELECT payout_id FROM payments WHERE appointment_id = '{appointment.Id}'")).ShouldBeNull();
        (await (await admin.GetAsync($"/api/v1/admin/finance/payout-batches/{first.Batch.Batch.Id}", Ct)).ReadAsync<PayoutBatchDetailDto>())
            .Batch.Status.ShouldBe(PayoutBatchStatus.Failed);
        (await admin.PostJsonAsync($"/api/v1/admin/finance/payouts/{failing.Id}/mark-paid", new { transferReference = "X" })).StatusCode
            .ShouldBe(HttpStatusCode.Conflict);

        var second = await (await admin.PostJsonAsync("/api/v1/admin/finance/payouts/run", new { date = Factory.Today().AddDays(-1) })).ReadAsync<PayoutRunDto>();
        var retry = second.Batch!.Payouts.ShouldHaveSingleItem();
        retry.AmountCents.ShouldBe(DoctorShare);

        var doctorClient = await Factory.ClientForAsync(doctor.UserId);
        var before = await (await doctorClient.GetAsync($"/api/v1/doctors/me/earnings?from={captureDate:yyyy-MM-dd}&to={captureDate:yyyy-MM-dd}", Ct))
            .ReadAsync<DoctorEarningsDto>();
        before.PendingPayoutCents.ShouldBe(DoctorShare);
        before.PaidCents.ShouldBe(0);

        var marked = await (await admin.PostJsonAsync($"/api/v1/admin/finance/payouts/{retry.Id}/mark-paid", new { transferReference = "BOC-778812" }))
            .ReadAsync<PayoutDto>();
        marked.Status.ShouldBe(PayoutStatus.Paid);
        marked.TransferReference.ShouldBe("BOC-778812");
        marked.PaidAt.ShouldNotBeNull();
        (await (await admin.GetAsync($"/api/v1/admin/finance/payout-batches/{second.Batch.Batch.Id}", Ct)).ReadAsync<PayoutBatchDetailDto>())
            .Batch.Status.ShouldBe(PayoutBatchStatus.Paid);

        var earnings = await (await doctorClient.GetAsync($"/api/v1/doctors/me/earnings?from={captureDate:yyyy-MM-dd}&to={captureDate:yyyy-MM-dd}", Ct))
            .ReadAsync<DoctorEarningsDto>();
        earnings.Payments.ShouldBe(1);
        earnings.GrossCents.ShouldBe(Fee);
        earnings.CommissionCents.ShouldBe(Commission);
        earnings.ProviderFeeCents.ShouldBe(ProviderFee);
        earnings.NetCents.ShouldBe(DoctorShare);
        earnings.RefundedCents.ShouldBe(0);
        earnings.PendingPayoutCents.ShouldBe(0);
        earnings.PaidCents.ShouldBe(DoctorShare);

        var mine = await (await doctorClient.GetAsync("/api/v1/doctors/me/payouts", Ct)).ReadAsync<PagedResult<DoctorPayoutDto>>();
        mine.Items.Select(p => p.Status).ShouldBe([PayoutStatus.Paid, PayoutStatus.Failed]);
    }

    private async Task<AdminRefundDto> RefundAsync(Guid appointmentId, long amountCents)
    {
        var admin = await Factory.AdminClientAsync(AdminRole.Finance);
        var refund = await (await admin.PostJsonAsync($"/api/v1/admin/payments/{await Fixture.PaymentIdAsync(appointmentId)}/refunds", new { amountCents, reason = "Goodwill" }))
            .ReadAsync<AdminRefundDto>(HttpStatusCode.Created);
        (await admin.PostAsync($"/api/v1/admin/finance/refunds/{refund.Id}/approve", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        return refund;
    }

    private async Task<PayoutRunDto> RunYesterdayAsync()
    {
        var admin = await Factory.AdminClientAsync(AdminRole.Finance);
        return await (await admin.PostJsonAsync("/api/v1/admin/finance/payouts/run", new { date = Factory.Today().AddDays(-1) })).ReadAsync<PayoutRunDto>();
    }

    private async Task MarkPaidAsync(Guid payoutId)
    {
        var admin = await Factory.AdminClientAsync(AdminRole.Finance);
        (await admin.PostJsonAsync($"/api/v1/admin/finance/payouts/{payoutId}/mark-paid", new { transferReference = "BOC-1" })).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private Task<Guid?> PayoutIdAsync(Guid appointmentId) => Fixture.ScalarAsync<Guid?>($"SELECT payout_id FROM payments WHERE appointment_id = '{appointmentId}'");

    [Fact]
    public async Task Only_settled_visits_past_the_hold_are_paid_and_refunds_in_flight_hold_their_payment_back()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var confirmed = await Factory.CapturedAsync(patient, doctor.DoctorId, 9);
        var refunded = await Fixture.CompletedAsync(patient, doctor.DoctorId, 10);
        var lateOutcome = await Factory.CapturedAsync(patient, doctor.DoctorId, 11);
        var refund = await RefundAsync(refunded.Id, 100_000);
        Factory.Time.Advance(TimeSpan.FromHours(20));
        await Fixture.MarkCompletedAsync(lateOutcome.Id);
        Factory.Time.Advance(TimeSpan.FromHours(10));

        // The late visit was captured 30h ago but only completed 10h ago; the other completed one has a refund still processing.
        var held = await RunYesterdayAsync();
        held.Created.ShouldBeFalse();

        await Factory.SettleAsync();
        Factory.Time.Advance(TimeSpan.FromHours(20));
        var run = await RunYesterdayAsync();

        var payout = run.Batch!.Payouts.ShouldHaveSingleItem();
        payout.PaymentCount.ShouldBe(2);
        payout.AmountCents.ShouldBe(2 * DoctorShare - refund.PayoutCents);
        (await PayoutIdAsync(refunded.Id)).ShouldBe(payout.Id);
        (await PayoutIdAsync(lateOutcome.Id)).ShouldBe(payout.Id);
        (await PayoutIdAsync(confirmed.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Refunds_after_a_payout_are_clawed_back_from_the_next_one_and_a_shortfall_carries_forward()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var start = Factory.Time.GetUtcNow();
        var first = await Fixture.CompletedAsync(patient, doctor.DoctorId, 9);
        Factory.Time.Advance(TimeSpan.FromHours(25));
        var p1 = (await RunYesterdayAsync()).Batch!.Payouts.ShouldHaveSingleItem();
        p1.AmountCents.ShouldBe(DoctorShare);
        await MarkPaidAsync(p1.Id);

        var clawback = await RefundAsync(first.Id, 100_000);
        await Factory.SettleAsync();
        (await Fixture.ScalarAsync<string>($"SELECT amount_cents || ':' || (applied_payout_id IS NULL) FROM payout_adjustments WHERE refund_id = '{clawback.Id}'"))
            .ShouldBe($"{-clawback.PayoutCents}:true");

        var second = await Fixture.CompletedAsync(patient, doctor.DoctorId, 10);
        var from = LocalDate(start);
        async Task<DoctorEarningsDto> EarningsAsync() =>
            await (await (await Factory.ClientForAsync(doctor.UserId)).GetAsync($"/api/v1/doctors/me/earnings?from={from:yyyy-MM-dd}&to={Factory.Today():yyyy-MM-dd}", Ct))
                .ReadAsync<DoctorEarningsDto>();
        var beforeRun = await EarningsAsync();
        beforeRun.PaidCents.ShouldBe(DoctorShare);
        beforeRun.PendingPayoutCents.ShouldBe(DoctorShare - clawback.PayoutCents);

        Factory.Time.Advance(TimeSpan.FromHours(25));
        var p2 = (await RunYesterdayAsync()).Batch!.Payouts.ShouldHaveSingleItem();
        p2.AmountCents.ShouldBe(DoctorShare - clawback.PayoutCents);
        var adjustment = p2.Adjustments.ShouldHaveSingleItem();
        adjustment.RefundId.ShouldBe(clawback.Id);
        adjustment.AmountCents.ShouldBe(-clawback.PayoutCents);
        (await Fixture.ScalarAsync<Guid?>($"SELECT applied_payout_id FROM payout_adjustments WHERE refund_id = '{clawback.Id}'")).ShouldBe(p2.Id);
        await MarkPaidAsync(p2.Id);

        var afterPaid = await EarningsAsync();
        afterPaid.PendingPayoutCents.ShouldBe(0);
        afterPaid.PaidCents.ShouldBe(2 * DoctorShare - clawback.PayoutCents);
        var mine = await (await (await Factory.ClientForAsync(doctor.UserId)).GetAsync("/api/v1/doctors/me/payouts", Ct)).ReadAsync<PagedResult<DoctorPayoutDto>>();
        mine.Items.Single(p => p.Id == p2.Id).Adjustments.ShouldHaveSingleItem().RefundId.ShouldBe(clawback.Id);
        mine.Items.Single(p => p.Id == p1.Id).Adjustments.ShouldBeEmpty();

        // A full refund of the second visit claws back its whole share; the third visit, itself partly refunded, cannot cover it.
        await RefundAsync(second.Id, Fee);
        var third = await Fixture.CompletedAsync(patient, doctor.DoctorId, 11);
        var partial = await RefundAsync(third.Id, 100_000);
        await Factory.SettleAsync();
        Factory.Time.Advance(TimeSpan.FromHours(25));
        (await RunYesterdayAsync()).Created.ShouldBeFalse();
        (await PayoutIdAsync(third.Id)).ShouldBeNull();

        var fourth = await Fixture.CompletedAsync(patient, doctor.DoctorId, 12);
        Factory.Time.Advance(TimeSpan.FromHours(25));
        var p3 = (await RunYesterdayAsync()).Batch!.Payouts.ShouldHaveSingleItem();
        p3.PaymentCount.ShouldBe(2);
        p3.AmountCents.ShouldBe(DoctorShare - partial.PayoutCents);
        (await PayoutIdAsync(fourth.Id)).ShouldBe(p3.Id);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM payout_adjustments WHERE applied_payout_id IS NULL")).ShouldBe(0);
    }

    [Fact]
    public async Task A_failed_payout_does_not_deduct_a_clawback_twice()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var appointment = await Fixture.CompletedAsync(patient, doctor.DoctorId, 9);
        Factory.Time.Advance(TimeSpan.FromHours(25));
        var p1 = (await RunYesterdayAsync()).Batch!.Payouts.ShouldHaveSingleItem();

        var refund = await RefundAsync(appointment.Id, 100_000);
        await Factory.SettleAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Finance);
        (await admin.PostJsonAsync($"/api/v1/admin/finance/payouts/{p1.Id}/mark-failed", new { reason = "Account closed" })).StatusCode.ShouldBe(HttpStatusCode.OK);

        Factory.Time.Advance(TimeSpan.FromHours(25));
        var retry = (await RunYesterdayAsync()).Batch!.Payouts.ShouldHaveSingleItem();
        retry.AmountCents.ShouldBe(DoctorShare - refund.PayoutCents);
        (await Fixture.ScalarAsync<Guid?>($"SELECT applied_payout_id FROM payout_adjustments WHERE refund_id = '{refund.Id}'")).ShouldBe(retry.Id);
    }

    [Fact]
    public async Task Payout_endpoints_need_finance_and_doctor_endpoints_need_a_doctor()
    {
        var support = await Factory.AdminClientAsync(AdminRole.Support);
        (await support.GetAsync("/api/v1/admin/finance/payout-batches", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var patient = await Factory.PatientAsync();
        (await patient.Client.GetAsync("/api/v1/doctors/me/payouts", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
