using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Infrastructure.Options;
using TeleMed.Infrastructure.Payments;

namespace TeleMed.Api.IntegrationTests;

// Ported from telemed-backend payment/provider/payhere/payhere_test.go.
public class PayHereSignatureTests
{
    private const string Merchant = "1221149";
    private const string Secret = "MzQ1Njc4OTAxMjM0NTY3ODkwMTIzNDU2Nzg5MA==";
    private const string Order = "3f1a6b7c-0000-4000-8000-000000000001";

    private static PayHereProvider Provider(string baseUrl = "https://sandbox.payhere.lk", HttpMessageHandler? handler = null) => new(
        new HttpClient(handler ?? new HttpClientHandler()),
        Microsoft.Extensions.Options.Options.Create(new PaymentsOptions
        {
            PayHere = new PayHereOptions
            {
                Enabled = true,
                MerchantId = Merchant,
                MerchantSecret = Secret,
                AppId = "app_123",
                AppSecret = "app_secret_456",
                BaseUrl = baseUrl,
                NotifyUrl = "https://api.example.lk/webhooks/payhere",
                ReturnUrl = "https://app.example.lk/return",
                CancelUrl = "https://app.example.lk/cancel",
            },
        }),
        new MemoryCache(new MemoryCacheOptions()),
        TimeProvider.System);

    private static Dictionary<string, string> Notify(string orderId, string amount, string currency, string status, string secret = Secret, string merchant = Merchant) => new()
    {
        ["merchant_id"] = merchant,
        ["order_id"] = orderId,
        ["payment_id"] = "320027000123",
        ["payhere_amount"] = amount,
        ["payhere_currency"] = currency,
        ["status_code"] = status,
        ["md5sig"] = PayHereSignature.NotifyHash(merchant, orderId, amount, currency, status, PayHereSignature.Md5Upper(secret)),
        ["method"] = "VISA",
        ["status_message"] = "Successfully completed",
    };

    private static void ShouldBeRejected(Dictionary<string, string> fields) =>
        Should.Throw<BadRequestException>(() => Provider().Verify(fields)).Code.ShouldBe("invalid_signature");

    [Fact]
    public void Hashes_match_the_protocol_vectors()
    {
        var secretHash = PayHereSignature.Md5Upper(Secret);
        secretHash.ShouldBe("5588F6A5B8604825021A8DA8A1498CCD");
        PayHereSignature.CheckoutHash(Merchant, "3f1a6b7c-0000-4000-8000-00000000000a", "1000.00", "LKR", secretHash).ShouldBe("A8A45E32CFBA5A485F76595C9E46A484");
        PayHereSignature.NotifyHash(Merchant, Order, "5000.00", "LKR", "2", secretHash).ShouldBe("178CEE8A874691F9B363E77BED922EA1");
    }

    [Fact]
    public void A_valid_notify_verifies_with_a_stable_event_id()
    {
        var fields = Notify(Order, "5000.00", "LKR", "2");

        var first = Provider().Verify(fields);
        var second = Provider().Verify(fields);

        first.Outcome.ShouldBe(PaymentNotificationOutcome.Succeeded);
        first.PaymentId.ShouldBe(Guid.Parse(Order));
        first.AmountCents.ShouldBe(500_000);
        first.Currency.ShouldBe("LKR");
        first.EventId.ShouldBe($"{Order}:320027000123:2");
        second.EventId.ShouldBe(first.EventId);
        first.PayloadJson.ShouldContain("\"method\":\"VISA\"");
    }

    [Theory]
    [InlineData("payhere_amount", "50000.00")]
    [InlineData("order_id", "3f1a6b7c-0000-4000-8000-0000000000ff")]
    [InlineData("payhere_currency", "USD")]
    [InlineData("status_code", "2")]
    public void Tampering_with_any_hashed_field_is_rejected(string field, string value)
    {
        var fields = Notify(Order, "5000.00", "LKR", "-2");
        fields[field] = value;
        ShouldBeRejected(fields);
    }

    [Fact]
    public void Wrong_secret_foreign_merchant_and_incomplete_notifies_are_rejected()
    {
        ShouldBeRejected(Notify(Order, "5000.00", "LKR", "2", secret: "not-our-secret"));
        ShouldBeRejected(Notify(Order, "5000.00", "LKR", "2", merchant: "9999999"));
        ShouldBeRejected([]);
        ShouldBeRejected(new() { ["merchant_id"] = Merchant, ["order_id"] = "x", ["status_code"] = "2" });
    }

    [Theory]
    [InlineData("3", PaymentNotificationOutcome.Authorized)]
    [InlineData("2", PaymentNotificationOutcome.Succeeded)]
    [InlineData("0", PaymentNotificationOutcome.Pending)]
    [InlineData("-1", PaymentNotificationOutcome.Failed)]
    [InlineData("-2", PaymentNotificationOutcome.Failed)]
    [InlineData("-3", PaymentNotificationOutcome.Chargeback)]
    [InlineData("7", PaymentNotificationOutcome.Ignored)]
    public void Status_codes_map_to_outcomes(string status, PaymentNotificationOutcome expected)
    {
        Provider().Verify(Notify(Order, "100.00", "LKR", status)).Outcome.ShouldBe(expected);
    }

    [Fact]
    public void Authorized_notifies_carry_the_token()
    {
        var fields = Notify(Order, "2500.00", "LKR", "3");
        fields["authorization_token"] = "auth_tok_12345";

        var notification = Provider().Verify(fields);

        notification.AuthorizationToken.ShouldBe("auth_tok_12345");
        notification.AmountCents.ShouldBe(250_000);
    }

    [Theory]
    [InlineData("-2", "2")]
    [InlineData("-1", "1")]
    public void The_currency_status_collision_is_real_and_refused(string genuineStatus, string forgedStatus)
    {
        var secretHash = PayHereSignature.Md5Upper(Secret);
        var genuine = PayHereSignature.NotifyHash(Merchant, Order, "5000.00", "LKR", genuineStatus, secretHash);
        PayHereSignature.NotifyHash(Merchant, Order, "5000.00", "LKR-", forgedStatus, secretHash).ShouldBe(genuine);

        Provider().Verify(Notify(Order, "5000.00", "LKR", genuineStatus)).Outcome.ShouldBe(PaymentNotificationOutcome.Failed);
        var forged = Notify(Order, "5000.00", "LKR", genuineStatus);
        forged["payhere_currency"] = "LKR-";
        forged["status_code"] = forgedStatus;
        forged["md5sig"].ShouldBe(genuine);
        ShouldBeRejected(forged);
    }

    [Theory]
    [InlineData(Order, "5000.00", "LKR-", "2")]
    [InlineData(Order, "5000.00", "LK", "R2")]
    [InlineData(Order, "5000.00", "LKRX", "2")]
    [InlineData(Order, "5000.00", "lkr", "2")]
    [InlineData(Order, "5000.00", "", "2")]
    [InlineData(Order, "5,000.00", "LKR", "2")]
    [InlineData(Order, "-5000.00", "LKR", "2")]
    [InlineData(Order, "5000.001", "LKR", "2")]
    [InlineData(Order, "5000.00L", "KRX", "2")]
    [InlineData(Order, "", "LKR", "2")]
    [InlineData("someone-elses-order-0000000000000000", "5000.00", "LKR", "2")]
    [InlineData("3f1a6b7c000040008000000000000001", "5000.00", "LKR", "2")]
    [InlineData("{3f1a6b7c-0000-4000-8000-000000000001}", "5000.00", "LKR", "2")]
    [InlineData("3F1A6B7C-0000-4000-8000-000000000001", "5000.00", "LKR", "2")]
    [InlineData(Order, "5000.00", "LKR", "SUCCESS")]
    [InlineData(Order, "5000.00", "LKR", "200")]
    [InlineData(Order, "٥000.00", "LKR", "2")]
    [InlineData(Order, "5000.00", "LKR", "٢")]
    [InlineData(Order, "5000.00", "LKR", "2\n")]
    public void Fields_outside_their_grammar_never_reach_the_hash(string orderId, string amount, string currency, string status)
    {
        ShouldBeRejected(Notify(orderId, amount, currency, status));
    }

    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(1, "0.01")]
    [InlineData(99, "0.99")]
    [InlineData(100, "1.00")]
    [InlineData(123456, "1234.56")]
    [InlineData(100000000, "1000000.00")]
    [InlineData(-150, "-1.50")]
    public void Amounts_format_with_two_decimals(long cents, string expected)
    {
        PayHereSignature.FormatAmount(cents).ShouldBe(expected);
    }

    [Fact]
    public void Amounts_round_trip()
    {
        for (long cents = 0; cents < 20_000; cents++)
        {
            PayHereSignature.ParseAmount(PayHereSignature.FormatAmount(cents)).ShouldBe(cents);
        }

        PayHereSignature.ParseAmount("1500.5").ShouldBe(150_050);
        PayHereSignature.ParseAmount("1500").ShouldBe(150_000);
    }

    [Fact]
    public void Checkout_is_signed_and_holds_only_off_sandbox()
    {
        var request = new PaymentIntentRequest(Guid.Parse(Order), 500_000, "LKR", AuthorizeOnly: true, "Telemedicine consultation", "saman@example.com", "+94771234567");

        var sandbox = Provider().CreateIntent(request);
        var live = Provider("https://www.payhere.lk").CreateIntent(request);
        var anonymous = Provider().CreateIntent(request with { AuthorizeOnly = false, CustomerEmail = null, CustomerPhone = null });

        sandbox.ActionUrl.ShouldBe("https://sandbox.payhere.lk/pay/checkout");
        live.ActionUrl.ShouldBe("https://www.payhere.lk/pay/authorize");
        anonymous.ActionUrl.ShouldBe("https://sandbox.payhere.lk/pay/checkout");
        sandbox.Fields!["amount"].ShouldBe("5000.00");
        sandbox.Fields["hash"].ShouldBe(PayHereSignature.CheckoutHash(Merchant, Order, "5000.00", "LKR", PayHereSignature.Md5Upper(Secret)));
        sandbox.Fields["email"].ShouldBe("saman@example.com");
        sandbox.Fields["phone"].ShouldBe("0771234567");
        anonymous.Fields!["email"].ShouldBe($"patient-{Order}@versalifehealth.com");
        anonymous.Fields["phone"].ShouldBe("0770000000");
    }

    [Fact]
    public void An_ambiguous_checkout_is_never_signed()
    {
        Should.Throw<InvalidOperationException>(() => Provider().CreateIntent(
            new PaymentIntentRequest(Guid.Parse(Order), 500_000, "LKR-", AuthorizeOnly: false, "x", null, null)));
        Should.Throw<InvalidOperationException>(() => Provider().CreateIntent(
            new PaymentIntentRequest(Guid.Parse(Order), 0, "LKR", AuthorizeOnly: false, "x", null, null)));
    }

    [Fact]
    public async Task Capture_uses_the_merchant_api_and_partial_refunds_are_refused()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/merchant/v1/oauth/token" => """{"access_token":"test_token","token_type":"bearer","expires_in":600}""",
            "/merchant/v1/payment/capture" when request.Headers.Authorization?.Parameter == "test_token" =>
                """{"status":1,"msg":"Successfully captured payment","data":{"status_code":2,"status_message":"Success","payment_id":320025527952}}""",
            _ => null,
        });
        var provider = Provider("https://payhere.test", handler);

        var capture = await provider.CaptureAsync(new ProviderCaptureRequest(Guid.Parse(Order), "auth_tok_abc", 2_500, "LKR"), TestContext.Current.CancellationToken);
        var partial = await provider.RefundAsync(
            new ProviderRefundRequest(Guid.Parse(Order), Guid.NewGuid(), "320025527952", 1_250, 2_500, "LKR"), TestContext.Current.CancellationToken);

        capture.ShouldBe(new ProviderResult(ProviderResultStatus.Succeeded, "320025527952"));
        handler.Bodies.ShouldContain(b => b.Contains("\"authorization_token\":\"auth_tok_abc\"") && b.Contains("\"amount\":25.00"));
        partial.Status.ShouldBe(ProviderResultStatus.NotSupported);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, string?> respond) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return respond(request) is { } body
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
        }
    }
}
