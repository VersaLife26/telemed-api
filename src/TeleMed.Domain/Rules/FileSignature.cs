namespace TeleMed.Domain.Rules;

public static class FileSignature
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Webp = "image/webp";
    public const string Pdf = "application/pdf";
    public const string Mp4 = "video/mp4";
    public const string Webm = "video/webm";

    public const int HeaderLength = 12;

    public static readonly IReadOnlySet<string> Images = new HashSet<string> { Jpeg, Png, Webp };
    public static readonly IReadOnlySet<string> Videos = new HashSet<string> { Mp4, Webm };

    public static string? DetectContentType(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return Jpeg;
        }

        if (header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return Png;
        }

        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return Webp;
        }

        if (header.StartsWith("%PDF-"u8))
        {
            return Pdf;
        }

        if (header.Length >= 8 && header[4..8].SequenceEqual("ftyp"u8))
        {
            return Mp4;
        }

        return header.StartsWith((ReadOnlySpan<byte>)[0x1A, 0x45, 0xDF, 0xA3]) ? Webm : null;
    }

    public static string Extension(string contentType) => contentType switch
    {
        Jpeg => ".jpg",
        Png => ".png",
        Webp => ".webp",
        Pdf => ".pdf",
        Mp4 => ".mp4",
        Webm => ".webm",
        _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, null),
    };

    public static string? ContentTypeFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => Jpeg,
        ".png" => Png,
        ".webp" => Webp,
        ".pdf" => Pdf,
        ".mp4" => Mp4,
        ".webm" => Webm,
        _ => null,
    };
}
