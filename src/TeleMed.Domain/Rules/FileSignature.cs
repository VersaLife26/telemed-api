namespace TeleMed.Domain.Rules;

public static class FileSignature
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Webp = "image/webp";
    public const string Pdf = "application/pdf";

    public const int HeaderLength = 12;

    public static readonly IReadOnlySet<string> Images = new HashSet<string> { Jpeg, Png, Webp };

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

        return header.StartsWith("%PDF-"u8) ? Pdf : null;
    }

    public static string Extension(string contentType) => contentType switch
    {
        Jpeg => ".jpg",
        Png => ".png",
        Webp => ".webp",
        Pdf => ".pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, null),
    };

    public static string? ContentTypeFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => Jpeg,
        ".png" => Png,
        ".webp" => Webp,
        ".pdf" => Pdf,
        _ => null,
    };
}
