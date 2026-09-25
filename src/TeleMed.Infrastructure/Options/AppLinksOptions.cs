namespace TeleMed.Infrastructure.Options;

public sealed class AppLinksOptions
{
    public const string Section = "AppLinks";

    // Absolute base URL of the patient web app; links are app-relative paths when empty.
    public string PatientAppUrl { get; set; } = "";

    public bool IsValid() =>
        PatientAppUrl.Length == 0
        || (Uri.TryCreate(PatientAppUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp));
}
