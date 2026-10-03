using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Pdf;

internal sealed class HmacMedicalReportSigner(IOptions<PrescriptionOptions> options) : IMedicalReportSigner
{
    public string Sign(MedicalReport report) => MedicalReportSignature.Sign(options.Value.KeyBytes, report);

    public bool Verify(MedicalReport report, string? providedHmac) =>
        MedicalReportSignature.Verify(options.Value.KeyBytes, report, providedHmac);

    public string VerifyUrl(Guid reportId, string hmac) =>
        $"{options.Value.VerifyBaseUrl.TrimEnd('/')}/mr/{reportId}?h={Uri.EscapeDataString(hmac)}";
}
