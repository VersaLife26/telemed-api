using TeleMed.Application.Abstractions;

namespace TeleMed.Infrastructure.Messaging;

internal sealed class DisabledSender : ISmsSender, IEmailSender
{
    public bool IsEnabled => false;

    public Task SendAsync(string phoneNumber, string message, CancellationToken ct) =>
        throw new InvalidOperationException("SMS is disabled (Sms:Provider=None).");

    public Task SendAsync(string to, string subject, string body, CancellationToken ct) =>
        throw new InvalidOperationException("Email is disabled (Email:Provider=None).");
}
