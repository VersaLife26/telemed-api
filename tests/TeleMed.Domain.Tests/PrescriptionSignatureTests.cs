using System.Text;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class PrescriptionSignatureTests
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("a-very-secret-key-that-is-long-enough");

    private static Prescription Sample(DateTimeOffset? issuedAt = null, params PrescriptionItem[] items) => new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        DoctorId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
        PatientId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
        DoctorName = "Nimal Perera",
        DoctorSlmc = "12345",
        IssuedAt = issuedAt ?? new DateTimeOffset(2026, 8, 20, 10, 30, 0, TimeSpan.Zero),
        Items = items.Length > 0 ? [.. items] : [Amoxicillin(), Paracetamol()],
    };

    private static PrescriptionItem Amoxicillin(string dosage = "1 capsule", int quantity = 21) => new()
    {
        DrugName = "Amoxicillin", Strength = "500mg", Form = "capsule", Dosage = dosage, Frequency = "3x daily",
        DurationDays = 7, Quantity = quantity, SortOrder = 0,
    };

    private static PrescriptionItem Paracetamol(int quantity = 10) => new()
    {
        DrugName = "Paracetamol", Strength = "500mg", Form = "tablet", Dosage = "1 tablet", Frequency = "as needed",
        DurationDays = 5, Quantity = quantity, Instructions = "After meals", IsGeneric = true, SortOrder = 1,
    };

    // Produced by the Go service's hmac.go for the same prescription, so both implementations agree byte for byte.
    [Theory]
    [InlineData(0L, "60b3a9a79127bc483abc170c09ea4708fbde1c7802988e7d6e2bdbc19972dacf")]
    [InlineData(1_234_560L, "ff645323d8a9b71727c3682d9c69954691f8b5635e0e2a667ccce5394857f113")]
    public void Matches_the_go_implementation(long extraTicks, string expected)
    {
        var prescription = Sample(new DateTimeOffset(2026, 8, 20, 10, 30, 0, TimeSpan.Zero).AddTicks(extraTicks));

        PrescriptionSignature.ItemsDigest(prescription.Items).ShouldBe("9488fdd36403107aa0f10099ddf34bfd46dfe40577a85598de25b884b5fe1f52");
        PrescriptionSignature.Sign(Key, prescription).ShouldBe(expected);
    }

    [Fact]
    public void Formats_issued_at_like_rfc3339_nano_in_utc()
    {
        var payload = PrescriptionSignature.CanonicalPayload(Sample(new DateTimeOffset(2026, 8, 20, 16, 0, 0, 123, TimeSpan.FromHours(5.5))));

        payload.ShouldContain("|2026-08-20T10:30:00.123Z|");
        payload.ShouldStartWith("v1|11111111-1111-1111-1111-111111111111|22222222-2222-2222-2222-222222222222|33333333-3333-3333-3333-333333333333|");
    }

    [Fact]
    public void A_freshly_signed_prescription_verifies_and_signing_is_deterministic()
    {
        var prescription = Sample();
        var hmac = PrescriptionSignature.Sign(Key, prescription);

        PrescriptionSignature.Sign(Key, prescription).ShouldBe(hmac);
        PrescriptionSignature.Verify(Key, prescription, hmac).ShouldBeTrue();
        PrescriptionSignature.Verify(Key, prescription, hmac.ToUpperInvariant()).ShouldBeTrue();
    }

    [Fact]
    public void Item_order_in_the_list_does_not_matter_only_sort_order_does()
    {
        var hmac = PrescriptionSignature.Sign(Key, Sample());

        PrescriptionSignature.Verify(Key, Sample(null, Paracetamol(), Amoxicillin()), hmac).ShouldBeTrue();
    }

    public static TheoryData<string, Func<Prescription, Prescription>> Tampering() => new()
    {
        { "dosage", _ => Sample(null, Amoxicillin(dosage: "2 capsules"), Paracetamol()) },
        { "quantity", _ => Sample(null, Amoxicillin(), Paracetamol(quantity: 100)) },
        { "extra item", _ => Sample(null, Amoxicillin(), Paracetamol(), new PrescriptionItem { DrugName = "Tramadol", Dosage = "1", Frequency = "2x", DurationDays = 5, Quantity = 10, SortOrder = 2 }) },
        { "missing item", _ => Sample(null, Amoxicillin()) },
        { "issued at", p => Sample(p.IssuedAt.AddHours(48)) },
        { "id", p => Copy(p, id: Guid.NewGuid()) },
        { "doctor", p => Copy(p, doctorId: Guid.NewGuid()) },
        { "patient", p => Copy(p, patientId: Guid.NewGuid()) },
        { "investigation", _ => Copy(Sample(), investigations: ["Full blood count"]) },
    };

    [Theory]
    [MemberData(nameof(Tampering))]
    public void Any_change_to_the_signed_content_is_detected(string change, Func<Prescription, Prescription> tamper)
    {
        var original = Sample();
        var hmac = PrescriptionSignature.Sign(Key, original);

        PrescriptionSignature.Verify(Key, tamper(original), hmac).ShouldBeFalse(change);
    }

    [Fact]
    public void A_different_key_fails()
    {
        var prescription = Sample();

        PrescriptionSignature.Verify("another-secret-key-that-is-long-enough"u8, prescription, PrescriptionSignature.Sign(Key, prescription)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-hex-at-all")]
    [InlineData("zz")]
    [InlineData("12345")]
    [InlineData("60b3a9a7")]
    public void Malformed_values_never_verify(string? provided)
    {
        PrescriptionSignature.Verify(Key, Sample(), provided).ShouldBeFalse();
    }

    [Fact]
    public void Pipes_inside_a_field_cannot_shift_one_item_set_onto_another()
    {
        var a = new PrescriptionItem { DrugName = "X|Y", Dosage = "1", Frequency = "1x", DurationDays = 1, Quantity = 1 };
        var b = new PrescriptionItem { DrugName = "X", Dosage = "Y|1", Frequency = "1x", DurationDays = 1, Quantity = 1 };

        PrescriptionSignature.ItemsDigest([a]).ShouldNotBe(PrescriptionSignature.ItemsDigest([b]));
    }

    [Fact]
    public void Issue_times_are_truncated_to_what_postgres_stores()
    {
        var value = new DateTimeOffset(2026, 8, 20, 10, 30, 0, TimeSpan.Zero).AddTicks(1_234_567);

        PrescriptionSignature.TruncateToMicroseconds(value).ShouldBe(value.AddTicks(-7));
    }

    private static Prescription Copy(
        Prescription p,
        Guid? id = null,
        Guid? doctorId = null,
        Guid? patientId = null,
        IReadOnlyList<string>? investigations = null) => new()
    {
        Id = id ?? p.Id,
        DoctorId = doctorId ?? p.DoctorId,
        PatientId = patientId ?? p.PatientId,
        DoctorName = p.DoctorName,
        DoctorSlmc = p.DoctorSlmc,
        IssuedAt = p.IssuedAt,
        Items = p.Items,
        Investigations = investigations is null ? p.Investigations : [.. investigations],
    };
}
