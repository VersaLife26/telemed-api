namespace TeleMed.Application.Abstractions;

public interface IEmailSender
{
    bool IsEnabled { get; }
    Task SendAsync(string to, string subject, string body, CancellationToken ct, IReadOnlyList<EmailAttachment>? attachments = null);
}
