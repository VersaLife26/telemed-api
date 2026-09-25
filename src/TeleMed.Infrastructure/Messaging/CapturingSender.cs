using Microsoft.Extensions.DependencyInjection;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Testing;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Messaging;

internal sealed class CapturingSender(IServiceScopeFactory scopes) : ISmsSender, IEmailSender
{
    public bool IsEnabled => true;

    public Task SendAsync(string phoneNumber, string message, CancellationToken ct) =>
        CaptureAsync(MessageChannel.Sms, phoneNumber, null, message, ct);

    public Task SendAsync(string to, string subject, string body, CancellationToken ct) =>
        CaptureAsync(MessageChannel.Email, to, subject, body, ct);

    // A separate scope keeps the capture write independent of whatever the caller has staged.
    private async Task CaptureAsync(MessageChannel channel, string recipient, string? subject, string body, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICapturedMessageRepository>().Add(new CapturedMessage
        {
            Channel = channel,
            Recipient = recipient,
            Subject = subject,
            Body = body,
        });
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(ct);
    }
}
