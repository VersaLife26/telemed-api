using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Infrastructure;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Pdf;

QuestPDF.Settings.License = LicenseType.Community;

var renderer = new QuestPrescriptionPdfRenderer(NullLogger<QuestPrescriptionPdfRenderer>.Instance);
var pdf = renderer.Render(new PrescriptionPdf(
    Id: Guid.Parse("8f3c2a10-4b6e-4d91-9c2a-1e7b5d4f8a21"),
    IssuedAt: new DateTimeOffset(2026, 10, 3, 10, 30, 0, TimeSpan.FromHours(5.5)),
    DoctorName: "Sumudu Weragoda",
    DoctorSlmc: "19782",
    DoctorQualifications: "MBBS (Colombo), MD (Family Medicine)\nGeneral Practitioner · Sri Lanka",
    PatientName: "Lelath Anuradha",
    PatientAgeYears: 34,
    PatientSex: "male",
    PatientWeightKg: 72,
    PatientAllergies: "Penicillin — confirm culture result before first dose",
    Items:
    [
        new PrescriptionPdfItem(
            "Augmentin",
            "625 mg",
            "Tablet",
            "1 tablet",
            "Twice a day",
            3,
            6,
            "Orally; After meals; After urine culture",
            false),
        new PrescriptionPdfItem(
            "Desatrol",
            "5 mg",
            "Tablet",
            "1 tablet",
            "Twice a day",
            5,
            10,
            "Orally",
            true),
    ],
    Investigations: ["Full/Complete Urine Report (FUR/CUR)"],
    Signature: null,
    Seal: null,
    VerifyUrl: "https://telemedicine.versalifehealth.com/prescriptions/verify?id=8f3c2a10-4b6e-4d91-9c2a-1e7b5d4f8a21",
    IsTest: false));

var dest = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "prescription-sample.pdf"));
Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
File.WriteAllBytes(dest, pdf);
Console.WriteLine($"Wrote {dest} ({pdf.Length} bytes)");
