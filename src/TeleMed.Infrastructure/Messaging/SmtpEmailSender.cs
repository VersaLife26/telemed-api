using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Messaging;

internal sealed class SmtpEmailSender(IOptions<EmailOptions> email, IOptions<AppLinksOptions> links) : IEmailSender
{
    public bool IsEnabled => true;

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct, IReadOnlyList<EmailAttachment>? attachments = null)
    {
        var o = email.Value;
        var (_, html) = BrandedEmailBody.Format(email, links, subject, body);
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(o.FromName, o.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        var alternative = new MultipartAlternative
        {
            new TextPart("plain") { Text = body },
            new TextPart("html") { Text = html },
        };
        if (attachments is { Count: > 0 })
        {
            var mixed = new Multipart("mixed") { alternative };
            foreach (var file in attachments)
            {
                var part = new MimePart(file.ContentType)
                {
                    Content = new MimeContent(new MemoryStream(file.Content)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = file.FileName,
                };
                mixed.Add(part);
            }

            message.Body = mixed;
        }
        else
        {
            message.Body = alternative;
        }

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
