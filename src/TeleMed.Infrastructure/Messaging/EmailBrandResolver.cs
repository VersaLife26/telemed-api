using Microsoft.Extensions.Options;
using TeleMed.Application.Notifications;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Messaging;

internal static class EmailBrandResolver
{
    public static EmailBrand Resolve(IOptions<EmailOptions> email, IOptions<AppLinksOptions> links)
    {
        var appUrl = links.Value.PatientAppUrl.TrimEnd('/');
        // Official VersaLife assets from the patient app (same files as the product UI).
        var headerLogo = string.IsNullOrWhiteSpace(email.Value.LogoUrl)
            ? $"{appUrl}/assets/logo-email.png"
            : email.Value.LogoUrl.Trim();
        var markLogo = string.IsNullOrWhiteSpace(email.Value.MarkLogoUrl)
            ? $"{appUrl}/assets/logo-mark-email.png"
            : email.Value.MarkLogoUrl.Trim();
        return new EmailBrand(email.Value.FromName, headerLogo, markLogo, appUrl);
    }
}
