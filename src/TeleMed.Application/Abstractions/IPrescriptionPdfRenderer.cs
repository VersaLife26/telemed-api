namespace TeleMed.Application.Abstractions;

public interface IPrescriptionPdfRenderer
{
    byte[] Render(PrescriptionPdf prescription);
}

public sealed record PrescriptionPdf(
    Guid Id,
    DateTimeOffset IssuedAt,
    string DoctorName,
    string DoctorSlmc,
    string DoctorQualifications,
    string PatientName,
    int? PatientAgeYears,
    string? PatientSex,
    decimal? PatientWeightKg,
    string? PatientAllergies,
    IReadOnlyList<PrescriptionPdfItem> Items,
    byte[]? Signature,
    byte[]? Seal,
    string VerifyUrl,
    bool IsTest);

public sealed record PrescriptionPdfItem(
    string DrugName, string Strength, string Form, string Dosage, string Frequency, int DurationDays, int Quantity, string? Instructions, bool IsGeneric);
