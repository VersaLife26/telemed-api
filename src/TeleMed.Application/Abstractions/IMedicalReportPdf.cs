using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Abstractions;

public interface IMedicalReportPdfRenderer
{
    byte[] Render(MedicalReportPdf report);
}

public sealed record MedicalReportPdf(
    Guid Id,
    DateTimeOffset IssuedAt,
    DateOnly VisitDate,
    string DoctorName,
    string DoctorSlmc,
    string DoctorQualifications,
    string PatientName,
    int? PatientAgeYears,
    string? PatientSex,
    string Addressee,
    string ClinicalImpression,
    string? Findings,
    string? Advice,
    FitnessForWork Fitness,
    DateOnly? LeaveFrom,
    DateOnly? LeaveUntil,
    DateOnly? ReturnToWorkOn,
    string? FitnessNotes,
    byte[]? Signature,
    byte[]? Seal,
    string VerifyUrl,
    bool IsTest);

public interface IMedicalReportSigner
{
    string Sign(MedicalReport report);
    bool Verify(MedicalReport report, string? providedHmac);
    string VerifyUrl(Guid reportId, string hmac);
}
