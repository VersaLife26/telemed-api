using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.Finance;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;
using static TeleMed.Api.IntegrationTests.Infrastructure.FinanceFlows;

namespace TeleMed.Api.IntegrationTests;

public class FinanceLedgerTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Ledger_totals_reconcile_with_payments_minus_refunds_and_leave_out_test_appointments()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        await Factory.CapturedAsync(patient, doctor.DoctorId, 9);
        var cancelled = await Factory.CapturedAsync(patient, doctor.DoctorId, 10);
        var test = await Factory.CapturedAsync(patient, doctor.DoctorId, 11);
        await Fixture.MarkTestAsync(test.Id);
        (await patient.Client.PostAsync($"/api/v1/appointments/{cancelled.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Factory.SettleAsync();

        var admin = await Factory.AdminClientAsync(AdminRole.Finance);
        var today = Factory.Today();
        var ledger = await (await admin.GetAsync($"/api/v1/admin/finance/ledger?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}", Ct))
            .ReadAsync<LedgerPageDto>();

        ledger.Total.ShouldBe(3);
        ledger.Items.Count(i => i.Type == LedgerEntryType.Payment).ShouldBe(2);
        ledger.Items.ShouldNotContain(i => i.AppointmentId == test.Id);
        var refund = ledger.Items.Single(i => i.Type == LedgerEntryType.Refund);
        refund.AppointmentId.ShouldBe(cancelled.Id);
        refund.AmountCents.ShouldBe(-Fee);
        refund.CommissionCents.ShouldBe(-Commission);

        var totals = ledger.Totals;
        totals.PaymentCount.ShouldBe(2);
        totals.RefundCount.ShouldBe(1);
        totals.CapturedCents.ShouldBe(2 * Fee);
        totals.RefundedCents.ShouldBe(Fee);
        totals.NetCents.ShouldBe(Fee);
        (totals.CommissionCents + totals.ProviderFeeCents + totals.PayoutCents).ShouldBe(totals.NetCents);
        totals.NetCents.ShouldBe(await Fixture.ScalarAsync<long>(
            "SELECT sum(p.captured_cents - p.refunded_cents)::bigint FROM payments p JOIN appointments a ON a.id = p.appointment_id WHERE NOT a.is_test"));
        totals.CommissionCents.ShouldBe(await Fixture.ScalarAsync<long>(
            "SELECT sum(p.commission_cents - p.refunded_commission_cents)::bigint FROM payments p JOIN appointments a ON a.id = p.appointment_id WHERE NOT a.is_test"));
        totals.PayoutCents.ShouldBe(DoctorShare);

        var paged = await (await admin.GetAsync($"/api/v1/admin/finance/ledger?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&pageSize=1&page=2", Ct))
            .ReadAsync<LedgerPageDto>();
        paged.Items.ShouldHaveSingleItem().Id.ShouldBe(ledger.Items[1].Id);
        paged.Totals.ShouldBe(totals);

        var otherDoctor = await (await admin.GetAsync($"/api/v1/admin/finance/ledger?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&doctorId={Guid.NewGuid()}", Ct))
            .ReadAsync<LedgerPageDto>();
        otherDoctor.Total.ShouldBe(0);
        otherDoctor.Totals.NetCents.ShouldBe(0);
    }

    [Fact]
    public async Task Ledger_csv_streams_text_csv_with_a_header_row()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var appointment = await Factory.CapturedAsync(patient, doctor.DoctorId, 9);
        var admin = await Factory.AdminClientAsync(AdminRole.SuperAdmin);

        var response = await admin.GetAsync("/api/v1/admin/finance/ledger.csv", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");
        response.Content.Headers.ContentDisposition!.FileName!.Trim('"').ShouldBe("ledger.csv");
        var lines = (await response.Content.ReadAsStringAsync(Ct)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].ShouldBe("type,id,payment_id,appointment_id,doctor_id,occurred_at,amount_cents,commission_cents,provider_fee_cents,payout_cents,currency,provider,reference");
        lines.Length.ShouldBe(2);
        lines[1].ShouldStartWith("payment,");
        lines[1].ShouldContain($",{appointment.Id},");
        lines[1].ShouldContain($",{Fee},{Commission},{ProviderFee},{DoctorShare},LKR,mock,");
    }

    [Fact]
    public async Task Commission_default_and_doctor_overrides_are_finance_only()
    {
        var finance = await Factory.AdminClientAsync(AdminRole.Finance);
        var commission = await (await finance.GetAsync("/api/v1/admin/finance/commission", Ct)).ReadAsync<CommissionDto>();
        commission.CommissionBps.ShouldBe(PlatformPolicy.CommissionBps);
        commission.ProviderFeeBps.ShouldBe(PlatformPolicy.ProviderFeeBps);
        commission.PayoutHoldHours.ShouldBe(24);
        commission.DoctorRates.ShouldBeEmpty();

        var updated = await (await finance.PutJsonAsync("/api/v1/admin/finance/commission", new { commissionBps = 1_500 }))
            .ReadAsync<CommissionDto>();
        updated.CommissionBps.ShouldBe(1_500);

        var doctor = await Factory.BookableDoctorAsync();
        var withDoctor = await (await finance.PutJsonAsync(
                $"/api/v1/admin/finance/commission/doctors/{doctor.DoctorId}", new { commissionBps = 1_000 }))
            .ReadAsync<CommissionDto>();
        withDoctor.DoctorRates.ShouldContain(r => r.DoctorId == doctor.DoctorId && r.CommissionBps == 1_000);

        var patient = await Factory.PatientAsync();
        var appointment = await Factory.CapturedAsync(patient, doctor.DoctorId, 9);
        var split = await Fixture.ScalarAsync<string>(
            $"SELECT commission_cents || ':' || provider_fee_cents || ':' || payout_cents FROM payments WHERE appointment_id = '{appointment.Id}'");
        split.ShouldBe("25000:7500:217500");

        var cleared = await (await finance.PutJsonAsync(
                $"/api/v1/admin/finance/commission/doctors/{doctor.DoctorId}", new { commissionBps = (int?)null }))
            .ReadAsync<CommissionDto>();
        cleared.DoctorRates.ShouldBeEmpty();
        cleared.CommissionBps.ShouldBe(1_500);

        (await (await Factory.AdminClientAsync(AdminRole.Ops)).GetAsync("/api/v1/admin/finance/commission", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await (await Factory.AdminClientAsync(AdminRole.Ops)).PutJsonAsync("/api/v1/admin/finance/commission", new { commissionBps = 1_000 }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
