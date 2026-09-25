using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Payments;

internal sealed class PayHereProvider(HttpClient http, IOptions<PaymentsOptions> options, IMemoryCache cache, TimeProvider time)
    : IPaymentProvider, IPayHereWebhookVerifier
{
    private const string TokenCacheKey = "payhere:merchant-api-token";

    private PayHereOptions Settings => options.Value.PayHere;

    private string BaseUrl => Settings.BaseUrl.TrimEnd('/');

    public PaymentProvider Provider => PaymentProvider.Payhere;

    public bool IsEnabled => Settings.Enabled;

    public PaymentIntent CreateIntent(PaymentIntentRequest request)
    {
        var orderId = request.PaymentId.ToString("D");
        var amount = PayHereSignature.FormatAmount(request.AmountCents);
        var currency = request.Currency.ToUpperInvariant();
        if (request.AmountCents <= 0 || !PayHereSignature.IsUnambiguous(orderId, amount, currency, "0"))
        {
            throw new InvalidOperationException("Refusing to sign a PayHere checkout whose hash preimage would be ambiguous.");
        }

        var fields = new Dictionary<string, string>
        {
            ["merchant_id"] = Settings.MerchantId,
            ["return_url"] = Settings.ReturnUrl,
            ["cancel_url"] = Settings.CancelUrl,
            ["notify_url"] = Settings.NotifyUrl,
            ["order_id"] = orderId,
            ["items"] = request.Description,
            ["currency"] = currency,
            ["amount"] = amount,
            ["hash"] = PayHereSignature.CheckoutHash(Settings.MerchantId, orderId, amount, currency, PayHereSignature.Md5Upper(Settings.MerchantSecret)),
            ["first_name"] = "Patient",
            ["last_name"] = "User",
            ["email"] = request.CustomerEmail is { } email && email.Contains('@') ? email : $"patient-{orderId}@versalifehealth.com",
            ["phone"] = LocalPhone(request.CustomerPhone),
            ["address"] = "Colombo",
            ["city"] = "Colombo",
            ["country"] = "Sri Lanka",
        };
        // Hold-on-card is a premium feature that sandbox merchants lack; there the hosted page errors, so charge at once.
        var path = request.AuthorizeOnly && !BaseUrl.Contains("sandbox", StringComparison.OrdinalIgnoreCase) ? "/pay/authorize" : "/pay/checkout";
        return new PaymentIntent(orderId, BaseUrl + path, fields, Succeeded: false);
    }

    public PaymentNotification Verify(IReadOnlyDictionary<string, string> fields)
    {
        string Field(string key) => fields.TryGetValue(key, out var value) ? value : "";

        var merchantId = Field("merchant_id");
        var orderId = Field("order_id");
        var amount = Field("payhere_amount");
        var currency = Field("payhere_currency");
        var statusCode = Field("status_code");
        var signature = Field("md5sig").Trim().ToUpperInvariant();
        var payHereId = Field("payment_id");

        if (merchantId.Length == 0 || orderId.Length == 0 || signature.Length == 0 || statusCode.Length == 0
            || !PayHereSignature.FixedTimeEquals(merchantId, Settings.MerchantId)
            // Before hashing, never after: the forged tuple carries a genuinely valid signature.
            || !PayHereSignature.IsUnambiguous(orderId, amount, currency, statusCode)
            || !PayHereSignature.FixedTimeEquals(
                PayHereSignature.NotifyHash(merchantId, orderId, amount, currency, statusCode, PayHereSignature.Md5Upper(Settings.MerchantSecret)),
                signature))
        {
            throw new BadRequestException("invalid_signature", "The payment notification could not be verified.");
        }

        var (outcome, failure) = statusCode switch
        {
            "3" => (PaymentNotificationOutcome.Authorized, null),
            "2" => (PaymentNotificationOutcome.Succeeded, null),
            "0" => (PaymentNotificationOutcome.Pending, null),
            "-1" => (PaymentNotificationOutcome.Failed, "cancelled at the payment page"),
            "-2" => (PaymentNotificationOutcome.Failed, Field("status_message") is { Length: > 0 } message ? message : "payment failed"),
            "-3" => (PaymentNotificationOutcome.Chargeback, null),
            _ => (PaymentNotificationOutcome.Ignored, (string?)null),
        };
        var token = Field("authorization_token").Trim();
        return new PaymentNotification(
            // PayHere sends no event id; this triple is stable across redeliveries and distinct between outcomes.
            $"{orderId}:{payHereId}:{statusCode}",
            Guid.ParseExact(orderId, "D"),
            outcome,
            PayHereSignature.ParseAmount(amount),
            currency,
            payHereId.Length > 0 ? payHereId : null,
            token.Length > 0 ? token : null,
            failure is { Length: > 500 } ? failure[..500] : failure,
            JsonSerializer.Serialize(fields));
    }

    public Task<ProviderResult> CaptureAsync(ProviderCaptureRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.AuthorizationToken))
        {
            return Task.FromResult(new ProviderResult(ProviderResultStatus.NotSupported, Message: "No authorization token to capture."));
        }

        return CallMerchantApiAsync("/merchant/v1/payment/capture", new
        {
            authorization_token = request.AuthorizationToken,
            amount = decimal.Parse(PayHereSignature.FormatAmount(request.AmountCents), CultureInfo.InvariantCulture),
            deduction_details = "Telemedicine consultation",
        }, requireCapturedStatus: true, ct);
    }

    // PayHere has no void endpoint: an uncaptured hold simply lapses, so releasing it is a no-op on our side.
    public Task<ProviderResult> VoidAsync(ProviderVoidRequest request, CancellationToken ct) =>
        Task.FromResult(new ProviderResult(ProviderResultStatus.Succeeded));

    // The refund endpoint refunds the whole payment and has no amount parameter. Refunding everything would hand
    // back money the policy keeps, so a partial refund is left for finance to do by hand in the PayHere portal.
    public Task<ProviderResult> RefundAsync(ProviderRefundRequest request, CancellationToken ct)
    {
        if (request.AmountCents != request.CapturedCents)
        {
            return Task.FromResult(new ProviderResult(ProviderResultStatus.NotSupported,
                Message: "PayHere refunds are full-amount only; refund this partial amount manually in the PayHere portal."));
        }

        return CallMerchantApiAsync("/merchant/v1/payment/refund", new
        {
            payment_id = request.ProviderPaymentId,
            description = $"telemed refund {request.RefundId}",
        }, requireCapturedStatus: false, ct);
    }

    private async Task<ProviderResult> CallMerchantApiAsync(string path, object body, bool requireCapturedStatus, CancellationToken ct)
    {
        try
        {
            var token = await AccessTokenAsync(ct);
            if (token is null)
            {
                return new ProviderResult(ProviderResultStatus.Unavailable, Message: "PayHere token endpoint unavailable.");
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path) { Content = JsonContent.Create(body) };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(message, ct);
            if ((int)response.StatusCode >= 500)
            {
                return new ProviderResult(ProviderResultStatus.Unavailable, Message: $"PayHere returned {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<MerchantApiResponse>(ct);
            if (result is null)
            {
                return new ProviderResult(ProviderResultStatus.Unavailable, Message: "PayHere response unusable.");
            }

            var ok = response.IsSuccessStatusCode && result.Status == 1 && (!requireCapturedStatus || result.Data?.StatusCode == 2);
            return ok
                ? new ProviderResult(ProviderResultStatus.Succeeded, result.Data?.PaymentId?.ToString())
                : new ProviderResult(ProviderResultStatus.Rejected, Message: result.Data?.StatusMessage ?? result.Msg ?? "PayHere rejected the request.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return new ProviderResult(ProviderResultStatus.Unavailable, Message: ex.Message);
        }
    }

    // PayHere rate-limits the token endpoint far harder than the API itself.
    private async Task<string?> AccessTokenAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(TokenCacheKey, out string? cached))
        {
            return cached;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/merchant/v1/oauth/token")
        {
            Content = new FormUrlEncodedContent([KeyValuePair.Create("grant_type", "client_credentials")]),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Settings.AppId}:{Settings.AppSecret}")));
        using var response = await http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode || await response.Content.ReadFromJsonAsync<TokenResponse>(ct) is not { AccessToken.Length: > 0 } token)
        {
            return null;
        }

        var lifetime = token.ExpiresIn > 60 ? TimeSpan.FromSeconds(token.ExpiresIn - 30) : TimeSpan.FromMinutes(5);
        cache.Set(TokenCacheKey, token.AccessToken, time.GetUtcNow() + lifetime);
        return token.AccessToken;
    }

    private static string LocalPhone(string? phone) => phone switch
    {
        null or "" => "0770000000",
        { Length: >= 12 } when phone.StartsWith("+94", StringComparison.Ordinal) => "0" + phone[3..],
        _ => phone,
    };

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] long ExpiresIn);

    private sealed record MerchantApiResponse(
        [property: JsonPropertyName("status")] int Status,
        [property: JsonPropertyName("msg")] string? Msg,
        [property: JsonPropertyName("data")] MerchantApiData? Data);

    private sealed record MerchantApiData(
        [property: JsonPropertyName("status_code")] int? StatusCode,
        [property: JsonPropertyName("status_message")] string? StatusMessage,
        [property: JsonPropertyName("payment_id")] JsonElement? PaymentId);
}
