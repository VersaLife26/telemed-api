using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TeleMed.Domain.Entities;

namespace TeleMed.Domain.Rules;

// Port of the Go service's hmac.go; prescriptions signed there must still verify here, so the payload is byte-for-byte identical.
// The HMAC covers the clinical content, not just the id, so an edited row no longer matches the value printed in the QR code.
public static class PrescriptionSignature
{
    private const string CanonicalVersion = "v1";

    public static string ItemsDigest(IEnumerable<PrescriptionItem> items)
    {
        var builder = new StringBuilder();
        foreach (var it in items.OrderBy(i => i.SortOrder))
        {
            builder.Append(CultureInfo.InvariantCulture,
                $"{it.DrugName}|{it.Strength}|{it.Form}|{it.Dosage}|{it.Frequency}|{it.DurationDays}|{it.Quantity}|{it.Instructions}|{(it.IsGeneric ? "true" : "false")}\n");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    public static string InvestigationsDigest(IEnumerable<string> investigations)
    {
        var builder = new StringBuilder();
        foreach (var line in investigations)
        {
            builder.Append(line).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    public static string CanonicalPayload(Prescription p)
    {
        var payload = string.Join('|', CanonicalVersion, p.Id.ToString("D"), p.DoctorId.ToString("D"), p.PatientId.ToString("D"), Rfc3339Nano(p.IssuedAt), ItemsDigest(p.Items));
        return p.Investigations.Count == 0
            ? payload
            : string.Join('|', "v2", p.Id.ToString("D"), p.DoctorId.ToString("D"), p.PatientId.ToString("D"), Rfc3339Nano(p.IssuedAt), ItemsDigest(p.Items), InvestigationsDigest(p.Investigations));
    }

    public static string Sign(ReadOnlySpan<byte> key, Prescription p) => Convert.ToHexStringLower(Mac(key, p));

    public static bool Verify(ReadOnlySpan<byte> key, Prescription p, string? providedHex)
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

        return provided.Length > 0 && CryptographicOperations.FixedTimeEquals(Mac(key, p), provided);
    }

    // Postgres keeps microseconds; signing anything finer would make every stored prescription fail verification.
    public static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value) =>
        new(value.UtcTicks - (value.UtcTicks % 10), TimeSpan.Zero);

    // Go's time.RFC3339Nano: UTC, trailing fractional zeros trimmed, no fraction at all for whole seconds.
    private static string Rfc3339Nano(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        var fraction = (utc.Ticks % TimeSpan.TicksPerSecond).ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + (fraction.Length > 0 ? "." + fraction : "") + "Z";
    }

    private static byte[] Mac(ReadOnlySpan<byte> key, Prescription p) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(CanonicalPayload(p)));
}
