using TeleMed.Domain.Enums;

namespace TeleMed.Application.Abstractions;

public interface IPaymentProvider
{
    PaymentProvider Provider { get; }
    bool IsEnabled { get; }

    // PayHere charges LKR on the domestic merchant and USD on the international merchant.
    bool CanCharge(string currency);

    // Local only (no network): PayHere signs a hosted-checkout form and the mock hands back an id.
    PaymentIntent CreateIntent(PaymentIntentRequest request);

    Task<ProviderResult> CaptureAsync(ProviderCaptureRequest request, CancellationToken ct);
    Task<ProviderResult> VoidAsync(ProviderVoidRequest request, CancellationToken ct);
    Task<ProviderResult> RefundAsync(ProviderRefundRequest request, CancellationToken ct);
}

public interface IPayHereWebhookVerifier
{
    bool IsEnabled { get; }

    // Throws BadRequestException("invalid_signature") for anything that is not a genuine notify for our merchant.
    PaymentNotification Verify(IReadOnlyDictionary<string, string> fields);
}

public sealed record PaymentIntentRequest(
    Guid PaymentId,
    long AmountCents,
    string Currency,
    bool AuthorizeOnly,
    string Description,
    string? CustomerEmail,
    string? CustomerPhone);

public sealed record PaymentIntent(string Reference, string? ActionUrl, IReadOnlyDictionary<string, string>? Fields, bool Succeeded);

public sealed record ProviderCaptureRequest(Guid PaymentId, string? AuthorizationToken, long AmountCents, string Currency);

public sealed record ProviderVoidRequest(Guid PaymentId, string? AuthorizationToken);

public sealed record ProviderRefundRequest(Guid PaymentId, Guid RefundId, string? ProviderPaymentId, long AmountCents, long CapturedCents, string Currency);

public enum ProviderResultStatus
{
    Succeeded,
    Rejected,
    Unavailable,
    // The provider cannot do this at all (e.g. a partial PayHere refund); someone has to do it by hand in the provider's portal.
    NotSupported,
}

public sealed record ProviderResult(ProviderResultStatus Status, string? Reference = null, string? Message = null);

public enum PaymentNotificationOutcome
{
    Authorized,
    Succeeded,
    Failed,
    Pending,
    Chargeback,
    Ignored,
}

public sealed record PaymentNotification(
    string EventId,
    Guid PaymentId,
    PaymentNotificationOutcome Outcome,
    long AmountCents,
    string Currency,
    string? ProviderPaymentId,
    string? AuthorizationToken,
    string? FailureReason,
    string PayloadJson);
