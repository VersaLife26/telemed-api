using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Infrastructure;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Enums;
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

var reportRenderer = new QuestMedicalReportPdfRenderer(NullLogger<QuestMedicalReportPdfRenderer>.Instance);
var report = reportRenderer.Render(new MedicalReportPdf(
    Id: Guid.Parse("7c1e9b44-2d8f-4a13-a6c0-91d5e3b8f012"),
    IssuedAt: new DateTimeOffset(2026, 10, 3, 10, 30, 0, TimeSpan.FromHours(5.5)),
    VisitDate: new DateOnly(2026, 10, 3),
    DoctorName: "Sumudu Weragoda",
    DoctorSlmc: "19742",
    DoctorQualifications: "MBBS (Colombo)\nGeneral Practitioner · Sri Lanka",
    PatientName: "Lelath Anuradha",
    PatientAgeYears: 34,
    PatientSex: "male",
    Addressee: "To whom it may concern",
    ClinicalImpression: "Acute viral illness",
    Findings: "History of fever, myalgia and reduced oral intake. No respiratory distress on this video examination.",
    Advice: "Drink more water. Rest. Return sooner if fever persists beyond 48 hours or breathing becomes difficult.",
    Fitness: FitnessForWork.Unfit,
    LeaveFrom: new DateOnly(2026, 10, 3),
    LeaveUntil: new DateOnly(2026, 10, 4),
    ReturnToWorkOn: new DateOnly(2026, 10, 5),
    FitnessNotes: "Please grant medical leave from routine work for the dates above.",
    Signature: null,
    Seal: null,
    VerifyUrl: "https://telemedicine.versalifehealth.com/mr/7c1e9b44-2d8f-4a13-a6c0-91d5e3b8f012",
    IsTest: false));

var reportDest = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "medical-report-sample.pdf"));
File.WriteAllBytes(reportDest, report);
Console.WriteLine($"Wrote {reportDest} ({report.Length} bytes)");
