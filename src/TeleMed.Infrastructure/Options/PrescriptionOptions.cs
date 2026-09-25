using System.Text;

namespace TeleMed.Infrastructure.Options;

public sealed class PrescriptionOptions
{
    public const string Section = "Prescriptions";
    public const int MinKeyBytes = 32;

    // Signs the QR code on every prescription; rotating it invalidates every prescription already printed.
    public string HmacKey { get; set; } = "";

    // The public page a pharmacist lands on; the QR code encodes {VerifyBaseUrl}/p/{id}?h={hmac}.
    public string VerifyBaseUrl { get; set; } = "";

    public byte[] KeyBytes => Encoding.UTF8.GetBytes(HmacKey);

    public bool HasValidVerifyBaseUrl() =>
        Uri.TryCreate(VerifyBaseUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
