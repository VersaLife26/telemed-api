using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Payments;

internal sealed class MockPaymentProvider(IOptions<PaymentsOptions> options) : IPaymentProvider
{
    public PaymentProvider Provider => PaymentProvider.Mock;

    public bool IsEnabled => options.Value.Mock.Enabled;

    public bool CanCharge(string currency) => true;

    public PaymentIntent CreateIntent(PaymentIntentRequest request) =>
        new($"mock_{request.PaymentId:N}", null, null, options.Value.Mock.AutoSucceed);

    public Task<ProviderResult> CaptureAsync(ProviderCaptureRequest request, CancellationToken ct) =>
        Task.FromResult(new ProviderResult(ProviderResultStatus.Succeeded, $"mock_capture_{request.PaymentId:N}"));

    public Task<ProviderResult> VoidAsync(ProviderVoidRequest request, CancellationToken ct) =>
        Task.FromResult(new ProviderResult(ProviderResultStatus.Succeeded, $"mock_void_{request.PaymentId:N}"));

    public Task<ProviderResult> RefundAsync(ProviderRefundRequest request, CancellationToken ct) =>
        Task.FromResult(new ProviderResult(ProviderResultStatus.Succeeded, $"mock_refund_{request.RefundId:N}"));
}
