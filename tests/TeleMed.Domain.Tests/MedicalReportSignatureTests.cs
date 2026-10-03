using System.Text;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class MedicalReportSignatureTests
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("a-very-secret-key-that-is-long-enough");

    private static MedicalReport Sample(string impression = "Acute viral illness") => new()
    {
        Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        DoctorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
        PatientId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
        DoctorName = "Sumudu Weragoda",
        DoctorSlmc = "19742",
        IssuedAt = new DateTimeOffset(2026, 10, 3, 10, 30, 0, TimeSpan.Zero),
        Addressee = "To whom it may concern",
        ClinicalImpression = impression,
        Findings = "Fever and myalgia. No respiratory distress.",
        Advice = "Drink more water. Rest.",
        Fitness = FitnessForWork.Unfit,
        LeaveFrom = new DateOnly(2026, 10, 3),
        LeaveUntil = new DateOnly(2026, 10, 4),
        ReturnToWorkOn = new DateOnly(2026, 10, 5),
        FitnessNotes = "Refrain from commitments and routine work.",
    };

    [Fact]
    public void A_freshly_signed_report_verifies_and_signing_is_deterministic()
    {
        var report = Sample();
        var hmac = MedicalReportSignature.Sign(Key, report);

        MedicalReportSignature.Sign(Key, report).ShouldBe(hmac);
        MedicalReportSignature.Verify(Key, report, hmac).ShouldBeTrue();
        MedicalReportSignature.Verify(Key, report, hmac.ToUpperInvariant()).ShouldBeTrue();
        MedicalReportSignature.CanonicalPayload(report).ShouldStartWith("mr1|");
    }

    [Fact]
    public void Changing_clinical_content_invalidates_the_signature()
    {
        var hmac = MedicalReportSignature.Sign(Key, Sample());
        MedicalReportSignature.Verify(Key, Sample("Different impression"), hmac).ShouldBeFalse();
    }
}
