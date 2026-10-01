namespace TeleMed.Infrastructure.Options;

public sealed class AppLinksOptions
{
    public const string Section = "AppLinks";

    // Absolute base URL of the patient web app; links are app-relative paths when empty.
    public string PatientAppUrl { get; set; } = "";

    // Doctor web origin for password-reset links. Falls back to PatientAppUrl when empty.
    public string DoctorAppUrl { get; set; } = "";

    public bool IsValid() => IsHttpUrl(PatientAppUrl) && IsHttpUrl(DoctorAppUrl);

    private static bool IsHttpUrl(string value) =>
        value.Length == 0
        || (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp));
}
