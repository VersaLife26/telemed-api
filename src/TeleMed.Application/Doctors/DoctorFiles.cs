using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Doctors;

internal sealed record BufferedUpload(byte[] Bytes, string ContentType, string Sha256);

internal static class DoctorFiles
{
    public static readonly IReadOnlySet<string> Documents = new HashSet<string> { FileSignature.Pdf, FileSignature.Png, FileSignature.Jpeg };
    public static readonly IReadOnlySet<string> Stamps = new HashSet<string> { FileSignature.Png, FileSignature.Jpeg };

    private const int MaxFileNameLength = 255;

    public static async Task<BufferedUpload> ReadAsync(Stream content, long maxBytes, IReadOnlySet<string> allowed, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0)
        {
            throw Invalid("The file is empty.");
        }

        if (buffer.Length > maxBytes)
        {
            throw Invalid($"The file must be at most {maxBytes / (1024 * 1024)} MB.");
        }

        var bytes = buffer.ToArray();
        var contentType = FileSignature.DetectContentType(bytes.AsSpan(0, Math.Min(bytes.Length, FileSignature.HeaderLength)));
        if (contentType is null || !allowed.Contains(contentType))
        {
            throw Invalid($"Only {string.Join(", ", allowed.Select(t => FileSignature.Extension(t).TrimStart('.').ToUpperInvariant()))} files are allowed.");
        }

        return new BufferedUpload(bytes, contentType, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    public static async Task<DoctorDocument> StoreAsync(
        IFileStorage storage,
        string keyPrefix,
        Guid? applicationId,
        Guid? doctorId,
        DoctorDocumentType type,
        BufferedUpload upload,
        string? fileName,
        CancellationToken ct)
    {
        var extension = FileSignature.Extension(upload.ContentType);
        var key = $"{keyPrefix}/{DocumentTypes.Name(type)}-{Guid.CreateVersion7()}{extension}";
        using var content = new MemoryStream(upload.Bytes, writable: false);
        await storage.SaveAsync(key, content, ct);
        return new DoctorDocument
        {
            ApplicationId = applicationId,
            DoctorId = doctorId,
            Type = type,
            StorageKey = key,
            FileName = CleanFileName(fileName, DocumentTypes.Name(type) + extension),
            ContentType = upload.ContentType,
            SizeBytes = upload.Bytes.LongLength,
            Sha256 = upload.Sha256,
        };
    }

    public static string ContentType(string storageKey) =>
        FileSignature.ContentTypeFromExtension(Path.GetExtension(storageKey)) ?? "application/octet-stream";

    public static ValidationException Invalid(string message, string property = "file") => new([new ValidationFailure(property, message)]);

    private static string CleanFileName(string? fileName, string fallback)
    {
        var name = new string(Path.GetFileName(fileName ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (name.Length == 0)
        {
            return fallback;
        }

        return name.Length <= MaxFileNameLength ? name : name[^MaxFileNameLength..];
    }
}

public static class DocumentTypes
{
    public static string Name(DoctorDocumentType type) => JsonNamingPolicy.CamelCase.ConvertName(type.ToString());

    public static DoctorDocumentType Parse(string value)
    {
        foreach (var type in Enum.GetValues<DoctorDocumentType>())
        {
            if (Name(type) == value)
            {
                return type;
            }
        }

        throw DoctorFiles.Invalid($"Unknown document type '{value}'.", "type");
    }
}
