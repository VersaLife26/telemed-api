using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Messaging;

internal sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public bool IsEnabled => true;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var o = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(o.Smtp.Host, o.Smtp.Port, o.Smtp.Security, ct);
        if (!string.IsNullOrEmpty(o.Smtp.Username))
        {
            await client.AuthenticateAsync(o.Smtp.Username, o.Smtp.Password ?? "", ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}
