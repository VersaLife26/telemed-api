using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Prescriptions;

public sealed record PrescriptionItemRequest(
    Guid? DrugId,
    string DrugName,
    string? Strength,
    string? Form,
    string Dosage,
    string Frequency,
    int DurationDays,
    int Quantity,
    string? Instructions,
    bool IsGeneric);

public sealed record IssuePrescriptionRequest(IReadOnlyList<PrescriptionItemRequest> Items);

public sealed record CancelPrescriptionRequest(string? Reason);

public sealed record PrescriptionItemDto(
    Guid? DrugId,
    string DrugName,
    string Strength,
    string Form,
    string Dosage,
    string Frequency,
    int DurationDays,
    int Quantity,
    string? Instructions,
    bool IsGeneric,
    int SortOrder);

public sealed record PrescriptionDto(
    Guid Id,
    Guid AppointmentId,
    Guid DoctorId,
    Guid PatientId,
    string DoctorName,
    string DoctorSlmc,
    string DoctorQualifications,
    DateTimeOffset IssuedAt,
    PrescriptionStatus Status,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    bool IsTest,
    IReadOnlyList<PrescriptionItemDto> Items);

public sealed record PrescriptionQuery : PageQuery;

public sealed record PrescriptionPdfFile(byte[] Content, string FileName);

public sealed record VerifiedItemDto(
    string DrugName, string Strength, string Form, string Dosage, string Frequency, int DurationDays, int Quantity, string? Instructions, bool IsGeneric);

// What a pharmacist scanning the QR code learns: enough to trust the paper in front of them and nothing that identifies the patient
// beyond initials. Details are only returned when the signature matches.
public sealed record PrescriptionVerificationDto(
    bool Valid,
    string? Reason,
    DateTimeOffset? IssuedAt,
    string? DoctorName,
    string? DoctorSlmc,
    string? PatientInitials,
    IReadOnlyList<VerifiedItemDto>? Items)
{
    public const string InvalidReason = "invalid";
    public const string TestReason = "test";
    public const string CancelledReason = "cancelled";

    public static PrescriptionVerificationDto Invalid { get; } = new(false, InvalidReason, null, null, null, null, null);
}
