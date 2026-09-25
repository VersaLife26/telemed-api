using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Pdf;

internal sealed class HmacPrescriptionSigner(IOptions<PrescriptionOptions> options) : IPrescriptionSigner
{
    public string Sign(Prescription prescription) => PrescriptionSignature.Sign(options.Value.KeyBytes, prescription);

    public bool Verify(Prescription prescription, string? providedHmac) => PrescriptionSignature.Verify(options.Value.KeyBytes, prescription, providedHmac);

    public string VerifyUrl(Guid prescriptionId, string hmac) =>
        $"{options.Value.VerifyBaseUrl.TrimEnd('/')}/p/{prescriptionId}?h={Uri.EscapeDataString(hmac)}";
}
