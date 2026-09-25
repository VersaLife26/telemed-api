using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Payments;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;
using TeleMed.Infrastructure.Payments;
using TeleMed.Infrastructure.Persistence;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;

namespace TeleMed.Api.IntegrationTests;

public class PaymentTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(BookableDoctor Doctor, Patient Patient, AppointmentDto Appointment)> BookAsync(TimeSpan? after = null)
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var start = await Factory.FreeSlotAsync(doctor.DoctorId, after ?? TimeSpan.FromHours(3));
        return (doctor, patient, await patient.Client.BookedAsync(doctor.DoctorId, start));
    }

    private async Task<string?> PaymentStateAsync(Guid appointmentId) =>
        await Fixture.ScalarAsync<string>($"SELECT status || ':' || coalesce(captured_cents::text, '') || ':' || refunded_cents FROM payments WHERE appointment_id = '{appointmentId}'");

    [Fact]
    public async Task Mock_payment_confirms_the_booking_with_a_card_hold_when_it_starts_within_six_days()
    {
        var (_, patient, booked) = await BookAsync();

        var summary = await (await patient.Client.GetAsync($"/api/v1/appointments/{booked.Id}/payment", Ct)).ReadAsync<OrderSummaryDto>();
        summary.AmountCents.ShouldBe(250_000);
        summary.AvailableProviders.ShouldBe([PaymentProvider.Payhere, PaymentProvider.Mock], ignoreOrder: true);

        var intent = await patient.Client.IntentAsync(booked.Id);
        intent.AuthorizeOnly.ShouldBeTrue();
        intent.Checkout.ShouldBeNull();
        var paid = await (await patient.Client.PostJsonAsync($"/api/v1/payments/{intent.PaymentId}/mock/complete", new { outcome = "succeed" })).ReadAsync<PaymentDto>();

        paid.Status.ShouldBe(PaymentStatus.Authorized);
        (await patient.Client.AppointmentAsync(booked.Id)).Status.ShouldBe(AppointmentStatus.Confirmed);
        var listed = await (await patient.Client.GetAsync("/api/v1/payments", Ct)).ReadAsync<PagedResult<PaymentDto>>();
        listed.Items.Select(p => p.Id).ShouldBe([paid.Id]);
        (await patient.Client.GetAsync($"/api/v1/payments/{paid.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await Factory.PatientAsync()).Client.GetAsync($"/api/v1/payments/{paid.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var again = await patient.Client.PostJsonAsync($"/api/v1/appointments/{booked.Id}/payment/intent", new { provider = "mock" });
        (await again.ProblemCodeAsync()).ShouldBe("payment_window_closed");
    }

    [Fact]
    public async Task Bookings_more_than_six_days_out_are_charged_at_once()
    {
        var (_, patient, booked) = await BookAsync(TimeSpan.FromDays(8));

        var paid = await patient.Client.PayWithMockAsync(booked.Id);

        paid.Status.ShouldBe(PaymentStatus.Succeeded);
        paid.CapturedCents.ShouldBe(250_000);
        paid.AuthorizeOnly.ShouldBeFalse();
    }

    [Fact]
    public async Task A_failed_mock_payment_leaves_the_booking_pending_and_can_be_retried()
    {
        var (_, patient, booked) = await BookAsync();

        var failed = await patient.Client.PayWithMockAsync(booked.Id, "fail");
        failed.Status.ShouldBe(PaymentStatus.Failed);
        (await patient.Client.AppointmentAsync(booked.Id)).Status.ShouldBe(AppointmentStatus.PendingPayment);

        (await patient.Client.PayWithMockAsync(booked.Id)).Status.ShouldBe(PaymentStatus.Authorized);
        (await patient.Client.AppointmentAsync(booked.Id)).Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task Disabled_providers_are_refused_and_mock_completion_is_hidden()
    {
        await using var factory = Factory.WithSettings(("Payments:Mock:Enabled", "false"));
        var (_, patient, booked) = await BookAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = patient.Client.DefaultRequestHeaders.Authorization;

        var intent = await client.PostJsonAsync($"/api/v1/appointments/{booked.Id}/payment/intent", new { provider = "mock" });
        intent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await intent.ProblemCodeAsync()).ShouldBe("provider_unavailable");

        var paymentId = await Fixture.ScalarAsync<Guid>($"SELECT id FROM payments WHERE appointment_id = '{booked.Id}'");
        (await client.PostJsonAsync($"/api/v1/payments/{paymentId}/mock/complete", new { outcome = "succeed" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Mock_auto_succeed_confirms_on_intent()
    {
        await using var factory = Factory.WithSettings(("Payments:Mock:AutoSucceed", "true"));
        var (_, patient, booked) = await BookAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = patient.Client.DefaultRequestHeaders.Authorization;

        var intent = await client.IntentAsync(booked.Id);

        intent.Status.ShouldBe(PaymentStatus.Authorized);
        (await patient.Client.AppointmentAsync(booked.Id)).Status.ShouldBe(AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task PayHere_intent_returns_a_signed_checkout_and_the_notify_confirms()
    {
        var (_, patient, booked) = await BookAsync();

        var intent = await patient.Client.IntentAsync(booked.Id, "payhere");

        intent.Checkout.ShouldNotBeNull();
        intent.Checkout.ActionUrl.ShouldBe("https://sandbox.payhere.lk/pay/checkout");
        var fields = intent.Checkout.Fields;
        fields["order_id"].ShouldBe(intent.PaymentId.ToString());
        fields["amount"].ShouldBe("2500.00");
        fields["hash"].ShouldBe(PayHereSignature.CheckoutHash(
            TeleMedApiFactory.PayHereMerchantId, intent.PaymentId.ToString(), "2500.00", "LKR", PayHereSignature.Md5Upper(TeleMedApiFactory.PayHereMerchantSecret)));
        fields["notify_url"].ShouldBe("https://api.telemed.test/api/v1/webhooks/payhere");

        (await Factory.PayHereNotifyAsync(intent.PaymentId, 250_000)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await patient.Client.AppointmentAsync(booked.Id)).Status.ShouldBe(AppointmentStatus.Confirmed);
        (await PaymentStateAsync(booked.Id)).ShouldBe("succeeded:250000:0");
        (await Fixture.ScalarAsync<string>("SELECT payload->>'payhere_amount' FROM payment_webhook_events")).ShouldBe("2500.00");
    }

    [Fact]
    public async Task Forged_mismatched_and_replayed_notifies_change_nothing_twice()
    {
        var (_, patient, booked) = await BookAsync();
        var intent = await patient.Client.IntentAsync(booked.Id, "payhere");

        var forged = await Factory.PayHereNotifyAsync(intent.PaymentId, 250_000, signature: new string('A', 32));
        forged.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await forged.ProblemCodeAsync()).ShouldBe("invalid_signature");

        var cheap = await Factory.PayHereNotifyAsync(intent.PaymentId, 100);
        cheap.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await cheap.ProblemCodeAsync()).ShouldBe("amount_mismatch");
        (await PaymentStateAsync(booked.Id)).ShouldBe("pending::0");

        (await Factory.PayHereNotifyAsync(intent.PaymentId, 250_000, statusCode: "3")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Factory.PayHereNotifyAsync(intent.PaymentId, 250_000, statusCode: "3")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Factory.PayHereNotifyAsync(intent.PaymentId, 250_000, statusCode: "-2")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await PaymentStateAsync(booked.Id)).ShouldBe("authorized::0");
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM payment_webhook_events")).ShouldBe(2);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM audit_logs WHERE entity_type = 'appointments' AND entity_id = '{booked.Id}' AND action = 'updated'"))
            .ShouldBe(1);

        var json = await Factory.CreateClient().PostJsonAsync("/api/v1/webhooks/payhere", new { order_id = intent.PaymentId });
        json.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Late_payment_after_expiry_reconfirms_when_the_time_is_still_free()
    {
        var (doctor, patient, booked) = await BookAsync();
        var intent = await patient.Client.IntentAsync(booked.Id, "payhere");
        Factory.Time.Advance(PlatformPolicy.PaymentWindow + TimeSpan.FromMinutes(1));
        await Factory.ExpireUnpaidAsync();
        (await Fixture.ScalarAsync<string>($"SELECT status FROM appointments WHERE id = '{booked.Id}'")).ShouldBe("cancelled");

        (await Factory.PayHereNotifyAsync(intent.PaymentId, 250_000)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var appointment = await (await Factory.ClientForAsync(patient.UserId)).AppointmentAsync(booked.Id);
        appointment.Status.ShouldBe(AppointmentStatus.Confirmed);
        appointment.CancelledAt.ShouldBeNull();
        (await PaymentStateAsync(booked.Id)).ShouldBe("succeeded:250000:0");
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM refunds")).ShouldBe(0);
        (await Factory.SlotsAsync(doctor.DoctorId)).Slots.Single(s => s.StartAt == booked.StartAt).Available.ShouldBeFalse();
    }

    [Fact]
    public async Task Late_payment_after_the_slot_was_rebooked_is_refunded_in_full()
    {
        var (doctor, patient, booked) = await BookAsync();
        var intent = await patient.Client.IntentAsync(booked.Id, "payhere");
        Factory.Time.Advance(PlatformPolicy.PaymentWindow + TimeSpan.FromMinutes(1));
        await Factory.ExpireUnpaidAsync();
        var rival = await Factory.PatientAsync();
        await rival.Client.BookedAsync(doctor.DoctorId, booked.StartAt);

        (await Factory.PayHereNotifyAsync(intent.PaymentId, 250_000)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var appointment = await (await Factory.ClientForAsync(patient.UserId)).AppointmentAsync(booked.Id);
        appointment.Status.ShouldBe(AppointmentStatus.Cancelled);
        appointment.RefundPercent.ShouldBe(100);
        (await Fixture.ScalarAsync<string>("SELECT concat_ws(':', status, reason, amount_cents, commission_cents, provider_fee_cents, payout_cents) FROM refunds"))
            .ShouldBe("processing:late_payment:250000:50000:7500:192500");
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM notifications WHERE template_key = 'payment_refunded' AND user_id = '{patient.UserId}'"))
            .ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Patient_cancelling_early_gets_a_full_refund()
    {
        var (_, patient, booked) = await BookAsync(TimeSpan.FromDays(8));
        await patient.Client.PayWithMockAsync(booked.Id);

        var cancelled = await (await patient.Client.PostJsonAsync($"/api/v1/appointments/{booked.Id}/cancel", new { reason = "Feeling better" }))
            .ReadAsync<AppointmentDto>();

        cancelled.Status.ShouldBe(AppointmentStatus.Cancelled);
        cancelled.CancelledBy.ShouldBe(CancellationActor.Patient);
        cancelled.RefundPercent.ShouldBe(100);
        (await Fixture.ScalarAsync<string>("SELECT concat_ws(':', status, reason, percent, amount_cents) FROM refunds"))
            .ShouldBe("processing:patient_cancellation:100:250000");

        await Factory.SettleAsync();

        (await PaymentStateAsync(booked.Id)).ShouldBe("refunded:250000:250000");
        (await Fixture.ScalarAsync<string>("SELECT status FROM refunds")).ShouldBe("succeeded");
        (await Fixture.ScalarAsync<string>($"SELECT concat_ws(':', refunded_commission_cents, refunded_provider_fee_cents, refunded_payout_cents) FROM payments WHERE appointment_id = '{booked.Id}'"))
            .ShouldBe("50000:7500:192500");
    }

    [Fact]
    public async Task Patient_cancelling_a_charged_booking_late_gets_half_back()
    {
        var (_, patient, booked) = await BookAsync(TimeSpan.FromDays(8));
        await patient.Client.PayWithMockAsync(booked.Id);
        Factory.Time.Advance(booked.StartAt - TimeSpan.FromHours(1) - Factory.Time.GetUtcNow());

        var cancelled = await (await (await Factory.ClientForAsync(patient.UserId)).PostAsync($"/api/v1/appointments/{booked.Id}/cancel", null, Ct))
            .ReadAsync<AppointmentDto>();
        await Factory.SettleAsync();

        cancelled.RefundPercent.ShouldBe(50);
        (await Fixture.ScalarAsync<string>("SELECT concat_ws(':', status, percent, amount_cents, commission_cents, provider_fee_cents, payout_cents) FROM refunds"))
            .ShouldBe("succeeded:50:125000:25000:3750:96250");
        (await PaymentStateAsync(booked.Id)).ShouldBe("partially_refunded:250000:125000");
    }

    [Fact]
    public async Task Late_cancel_on_a_card_hold_captures_half_and_early_cancel_voids_it()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var late = await patient.Client.BookedAsync(doctor.DoctorId, await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromMinutes(40)));
        var early = await patient.Client.BookedAsync(doctor.DoctorId, await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(5)));
        await patient.Client.PayWithMockAsync(late.Id);
        await patient.Client.PayWithMockAsync(early.Id);

        (await (await patient.Client.PostAsync($"/api/v1/appointments/{late.Id}/cancel", null, Ct)).ReadAsync<AppointmentDto>()).RefundPercent.ShouldBe(50);
        (await (await patient.Client.PostAsync($"/api/v1/appointments/{early.Id}/cancel", null, Ct)).ReadAsync<AppointmentDto>()).RefundPercent.ShouldBe(100);
        await Factory.SettleAsync();

        (await PaymentStateAsync(late.Id)).ShouldBe("succeeded:125000:0");
        (await Fixture.ScalarAsync<string>($"SELECT concat_ws(':', commission_cents, provider_fee_cents, payout_cents) FROM payments WHERE appointment_id = '{late.Id}'"))
            .ShouldBe("25000:3750:96250");
        (await PaymentStateAsync(early.Id)).ShouldBe("voided::0");
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM refunds")).ShouldBe(0);
    }

    [Fact]
    public async Task Cancelling_an_unpaid_booking_fails_the_payment_and_frees_the_slot()
    {
        var (doctor, patient, booked) = await BookAsync();

        var cancelled = await (await patient.Client.PostAsync($"/api/v1/appointments/{booked.Id}/cancel", null, Ct)).ReadAsync<AppointmentDto>();

        cancelled.RefundPercent.ShouldBeNull();
        (await PaymentStateAsync(booked.Id)).ShouldBe("failed::0");
        (await Factory.SlotsAsync(doctor.DoctorId)).Slots.Single(s => s.StartAt == booked.StartAt).Available.ShouldBeTrue();
        (await (await patient.Client.PostAsync($"/api/v1/appointments/{booked.Id}/cancel", null, Ct)).ProblemCodeAsync()).ShouldBe("invalid_transition");
    }

    [Fact]
    public async Task A_partial_PayHere_refund_is_left_for_manual_processing()
    {
        var (_, patient, booked) = await BookAsync(TimeSpan.FromDays(8));
        var intent = await patient.Client.IntentAsync(booked.Id, "payhere");
        (await Factory.PayHereNotifyAsync(intent.PaymentId, 250_000)).StatusCode.ShouldBe(HttpStatusCode.OK);
        Factory.Time.Advance(booked.StartAt - TimeSpan.FromHours(1) - Factory.Time.GetUtcNow());
        (await (await Factory.ClientForAsync(patient.UserId)).PostAsync($"/api/v1/appointments/{booked.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await Factory.SettleAsync();

        (await Fixture.ScalarAsync<string>("SELECT concat_ws(':', status, percent, amount_cents) FROM refunds")).ShouldBe("manual_required:50:125000");
        (await Fixture.ScalarAsync<string>("SELECT failure_reason FROM refunds"))!.ShouldContain("PayHere portal");
        (await PaymentStateAsync(booked.Id)).ShouldBe("succeeded:250000:0");
        (await Fixture.ScalarAsync<string>("SELECT concat_ws('|', kind, href) FROM admin_notifications WHERE kind = 'refund_manual_required'"))
            .ShouldBe($"refund_manual_required|/appointments/{booked.Id}");
    }

    [Fact]
    public async Task Capture_retries_stop_after_the_attempt_cap()
    {
        var (_, patient, booked) = await BookAsync(TimeSpan.FromMinutes(40));
        var intent = await patient.Client.IntentAsync(booked.Id, "payhere");
        (await Factory.PayHereNotifyAsync(intent.PaymentId, 250_000, statusCode: "3")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await patient.Client.PostAsync($"/api/v1/appointments/{booked.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var state = $"SELECT concat_ws(':', status, capture_attempts, capture_failed_at IS NOT NULL) FROM payments WHERE appointment_id = '{booked.Id}'";

        await Factory.SettleAsync();
        (await Fixture.ScalarAsync<string>(state)).ShouldBe("authorized:1:f");

        for (var i = 1; i < PlatformPolicy.MaxCaptureAttempts + 3; i++)
        {
            await Factory.SettleAsync();
        }

        (await Fixture.ScalarAsync<string>(state)).ShouldBe($"authorized:{PlatformPolicy.MaxCaptureAttempts}:t");
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM admin_notifications WHERE kind = 'payment_capture_failed'")).ShouldBe(1);
    }

    private async Task<PromoCode> PromoAsync(string code, int? maxRedemptions = null, int maxPerUser = 1, int percentBps = 1_000, long minAmount = 0)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var promo = new PromoCode
        {
            Code = code,
            DiscountType = PromoDiscountType.Percent,
            PercentBps = percentBps,
            MinAmountCents = minAmount,
            ValidFrom = Factory.Time.GetUtcNow().AddDays(-1),
            MaxRedemptions = maxRedemptions,
            MaxPerUser = maxPerUser,
        };
        db.PromoCodes.Add(promo);
        await db.SaveChangesAsync(Ct);
        return promo;
    }

    private static Task<HttpResponseMessage> ApplyPromoAsync(HttpClient client, Guid appointmentId, string code) =>
        client.PutJsonAsync($"/api/v1/appointments/{appointmentId}/payment/promo", new { code });

    [Fact]
    public async Task Promo_codes_discount_the_order_and_can_be_removed()
    {
        var (_, patient, booked) = await BookAsync();
        await PromoAsync("WELCOME10");

        var applied = await (await ApplyPromoAsync(patient.Client, booked.Id, " welcome10 ")).ReadAsync<OrderSummaryDto>();
        applied.DiscountCents.ShouldBe(25_000);
        applied.AmountCents.ShouldBe(225_000);
        applied.PromoCode.ShouldBe("WELCOME10");
        applied.PromoExpiresAt.ShouldBe(Factory.Time.GetUtcNow() + PlatformPolicy.PromoReservationTtl);
        (await Fixture.ScalarAsync<string>($"SELECT concat_ws(':', commission_cents, provider_fee_cents, payout_cents) FROM payments WHERE appointment_id = '{booked.Id}'"))
            .ShouldBe("45000:6750:173250");

        var removed = await (await patient.Client.DeleteAsync($"/api/v1/appointments/{booked.Id}/payment/promo", Ct)).ReadAsync<OrderSummaryDto>();
        removed.AmountCents.ShouldBe(250_000);
        removed.PromoCode.ShouldBeNull();

        (await (await ApplyPromoAsync(patient.Client, booked.Id, "NOPE")).ProblemCodeAsync()).ShouldBe("promo_unknown");
        (await (await patient.Client.DeleteAsync($"/api/v1/appointments/{booked.Id}/payment/promo", Ct)).ProblemCodeAsync()).ShouldBe("no_promo");
    }

    [Fact]
    public async Task Promo_budgets_are_enforced_per_user_and_globally()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var alice = await Factory.PatientAsync();
        var bob = await Factory.PatientAsync();
        var slot = await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3));
        var aliceFirst = await alice.Client.BookedAsync(doctor.DoctorId, slot);
        var aliceSecond = await alice.Client.BookedAsync(doctor.DoctorId, slot.AddMinutes(30));
        var bobs = await bob.Client.BookedAsync(doctor.DoctorId, slot.AddMinutes(60));
        await PromoAsync("ONCE", maxPerUser: 1);
        await PromoAsync("SINGLE", maxRedemptions: 1, maxPerUser: 5);
        await PromoAsync("BIGSPEND", minAmount: 1_000_000);

        (await ApplyPromoAsync(alice.Client, aliceFirst.Id, "ONCE")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApplyPromoAsync(alice.Client, aliceFirst.Id, "ONCE")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await ApplyPromoAsync(alice.Client, aliceSecond.Id, "ONCE")).ProblemCodeAsync()).ShouldBe("promo_exhausted");
        (await ApplyPromoAsync(bob.Client, bobs.Id, "ONCE")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await ApplyPromoAsync(alice.Client, aliceSecond.Id, "SINGLE")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await (await ApplyPromoAsync(bob.Client, bobs.Id, "SINGLE")).ProblemCodeAsync()).ShouldBe("promo_exhausted");
        (await (await ApplyPromoAsync(bob.Client, bobs.Id, "BIGSPEND")).ProblemCodeAsync()).ShouldBe("promo_not_applicable");

        Factory.Time.Advance(PlatformPolicy.PromoReservationTtl + TimeSpan.FromSeconds(1));
        await Factory.SettleAsync();
        var bobClient = await Factory.ClientForAsync(bob.UserId);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM promo_redemptions WHERE status = 'reserved'")).ShouldBe(0);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM payments WHERE discount_cents > 0")).ShouldBe(0);
        (await ApplyPromoAsync(bobClient, bobs.Id, "SINGLE")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Promo_is_locked_once_payment_starts_and_consumed_on_success()
    {
        var (_, patient, booked) = await BookAsync();
        await PromoAsync("WELCOME10");
        await ApplyPromoAsync(patient.Client, booked.Id, "WELCOME10");

        var intent = await patient.Client.IntentAsync(booked.Id);
        intent.AmountCents.ShouldBe(225_000);
        var locked = await patient.Client.DeleteAsync($"/api/v1/appointments/{booked.Id}/payment/promo", Ct);
        locked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await locked.ProblemCodeAsync()).ShouldBe("promo_locked");

        await patient.Client.PostJsonAsync($"/api/v1/payments/{intent.PaymentId}/mock/complete", new { outcome = "succeed" });

        (await Fixture.ScalarAsync<string>("SELECT status FROM promo_redemptions")).ShouldBe("consumed");
    }

    [Fact]
    public async Task A_failed_payment_releases_the_promo()
    {
        var (_, patient, booked) = await BookAsync();
        await PromoAsync("WELCOME10");
        await ApplyPromoAsync(patient.Client, booked.Id, "WELCOME10");

        await patient.Client.PayWithMockAsync(booked.Id, "fail");

        (await Fixture.ScalarAsync<string>("SELECT status || ':' || release_reason FROM promo_redemptions")).ShouldBe("released:payment_failed");
        (await (await patient.Client.GetAsync($"/api/v1/appointments/{booked.Id}/payment", Ct)).ReadAsync<OrderSummaryDto>()).AmountCents.ShouldBe(250_000);
    }
}
