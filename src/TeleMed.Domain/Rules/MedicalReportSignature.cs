using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TeleMed.Domain.Entities;

namespace TeleMed.Domain.Rules;

public static class MedicalReportSignature
{
    private const string CanonicalVersion = "mr1";

    public static string ContentDigest(MedicalReport report)
    {
        var builder = new StringBuilder();
        builder.Append(report.Addressee).Append('|');
        builder.Append(report.ClinicalImpression).Append('|');
        builder.Append(report.Findings).Append('|');
        builder.Append(report.Advice).Append('|');
        builder.Append(report.Fitness.ToString()).Append('|');
        builder.Append(Iso(report.LeaveFrom)).Append('|');
        builder.Append(Iso(report.LeaveUntil)).Append('|');
        builder.Append(Iso(report.ReturnToWorkOn)).Append('|');
        builder.Append(report.FitnessNotes);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    public static string CanonicalPayload(MedicalReport report) =>
        string.Join('|',
            CanonicalVersion,
            report.Id.ToString("D"),
            report.DoctorId.ToString("D"),
            report.PatientId.ToString("D"),
            Rfc3339Nano(report.IssuedAt),
            ContentDigest(report));

    public static string Sign(ReadOnlySpan<byte> key, MedicalReport report) =>
        Convert.ToHexStringLower(Mac(key, report));

    public static bool Verify(ReadOnlySpan<byte> key, MedicalReport report, string? providedHex)
    {
        byte[] provided;
        try
        {
            provided = Convert.FromHexString(providedHex ?? "");
        }
        catch (FormatException)
        {
            return false;
        }

        return provided.Length > 0 && CryptographicOperations.FixedTimeEquals(Mac(key, report), provided);
    }

    private static string Iso(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    private static string Rfc3339Nano(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        var fraction = (utc.Ticks % TimeSpan.TicksPerSecond).ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + (fraction.Length > 0 ? "." + fraction : "") + "Z";
    }

    private static byte[] Mac(ReadOnlySpan<byte> key, MedicalReport report) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(CanonicalPayload(report)));
}
