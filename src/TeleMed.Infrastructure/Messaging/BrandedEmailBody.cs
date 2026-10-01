using Microsoft.Extensions.Options;
using TeleMed.Application.Notifications;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Messaging;

/// <summary>
/// Applies the VersaLife HTML shell to plain-text bodies before SMTP delivery.
/// </summary>
internal static class BrandedEmailBody
{
    public static (string Plain, string Html) Format(IOptions<EmailOptions> email, IOptions<AppLinksOptions> links, string subject, string plainBody)
    {
        var brand = EmailBrandResolver.Resolve(email, links);
        var html = EmailHtmlLayout.Render(brand, subject, plainBody);
        return (plainBody, html);
    }
}
