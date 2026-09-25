using TeleMed.Domain.Entities;

namespace TeleMed.Application.Abstractions;

public interface IPrescriptionSigner
{
    string Sign(Prescription prescription);
    bool Verify(Prescription prescription, string? providedHmac);
    // The public page a pharmacist opens from the QR code.
    string VerifyUrl(Guid prescriptionId, string hmac);
}
