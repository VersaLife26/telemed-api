using TeleMed.Application.Abstractions;
using TeleMed.Application.Notifications.Templates;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Notifications;

public sealed record NotificationRecipient(string? Email, string? Phone, Language Language, Guid? UserId = null);

// Renders and stages outbox rows in the caller's unit of work; NotificationDispatcherJob sends them after commit.
public interface INotificationService
{
    Task EnqueueAsync(Guid userId, INotificationModel model, string dedupeKey, CancellationToken ct, EmailAttachment? emailAttachment = null);
    Task EnqueueAsync(NotificationRecipient recipient, INotificationModel model, string dedupeKey, CancellationToken ct, EmailAttachment? emailAttachment = null);
}

internal sealed class NotificationService(
    INotificationRepository notifications,
    IUserAccounts users,
    ISmsSender sms,
    IEmailSender email,
    TimeProvider time) : INotificationService
{
    public async Task EnqueueAsync(Guid userId, INotificationModel model, string dedupeKey, CancellationToken ct, EmailAttachment? emailAttachment = null)
    {
        if (await users.FindByIdAsync(userId, ct) is not { Status: not UserStatus.Deleted } user)
        {
            return;
        }

        await EnqueueAsync(new NotificationRecipient(user.Email, user.PhoneNumber, user.Language, user.Id), model, dedupeKey, ct, emailAttachment);
    }

    public async Task EnqueueAsync(NotificationRecipient recipient, INotificationModel model, string dedupeKey, CancellationToken ct, EmailAttachment? emailAttachment = null)
    {
        var template = NotificationTemplates.All[model.TemplateKey];
        var values = model.Values();
        var rows = new List<Notification>();
        if (email.IsEnabled && !string.IsNullOrWhiteSpace(recipient.Email) && template.RenderEmail(recipient.Language, values) is { } rendered)
        {
            rows.Add(Row(recipient, MessageChannel.Email, recipient.Email, rendered.Subject, rendered.Body, emailAttachment));
        }

        if (sms.IsEnabled && !string.IsNullOrWhiteSpace(recipient.Phone) && template.RenderSms(recipient.Language, values) is { } text)
        {
            rows.Add(Row(recipient, MessageChannel.Sms, recipient.Phone, null, text, null));
        }

        if (rows.Count == 0)
        {
            return;
        }

        var existing = await notifications.ExistingDedupeKeysAsync(rows.Select(r => r.DedupeKey).ToList(), ct);
        foreach (var row in rows.Where(r => !existing.Contains(r.DedupeKey)))
        {
            notifications.Add(row);
        }

        Notification Row(NotificationRecipient to, MessageChannel channel, string address, string? subject, string body, EmailAttachment? attachment) => new()
        {
            UserId = to.UserId,
            Channel = channel,
            TemplateKey = template.Key,
            Locale = to.Language,
            Recipient = address,
            Subject = subject,
            Body = body,
            AttachmentContent = channel == MessageChannel.Email ? attachment?.Content : null,
            AttachmentFileName = channel == MessageChannel.Email ? attachment?.FileName : null,
            NextAttemptAt = time.GetUtcNow(),
            DedupeKey = $"{dedupeKey}:{channel.ToString().ToLowerInvariant()}",
        };
    }
}
