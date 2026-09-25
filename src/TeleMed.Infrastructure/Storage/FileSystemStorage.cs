using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Storage;

internal sealed class FileSystemStorage(IOptions<StorageOptions> options, TimeProvider time) : IFileStorage
{
    public const string UrlPrefix = "/api/v1/files/";

    public TimeSpan SignedUrlLifetime => options.Value.UrlTtl;

    public async Task SaveAsync(string key, Stream content, CancellationToken ct)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        await content.CopyToAsync(file, ct);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        File.Delete(PathFor(key));
        return Task.CompletedTask;
    }

    public Stream? OpenRead(string key)
    {
        var path = PathFor(key);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public string CreateSignedUrl(string key, string contentType)
    {
        var expires = (time.GetUtcNow() + options.Value.UrlTtl).ToUnixTimeSeconds();
        var payload = Encoding.UTF8.GetBytes(string.Join('\n', expires.ToString(CultureInfo.InvariantCulture), contentType, key));
        return UrlPrefix + Base64Url.EncodeToString(payload) + "." + Base64Url.EncodeToString(Sign(payload));
    }

    public StoredFile? OpenSigned(string token)
    {
        var dot = token.IndexOf('.');
        if (dot <= 0)
        {
            return null;
        }

        byte[] payload, signature;
        try
        {
            payload = Base64Url.DecodeFromChars(token.AsSpan(0, dot));
            signature = Base64Url.DecodeFromChars(token.AsSpan(dot + 1));
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(signature, Sign(payload)))
        {
            return null;
        }

        var parts = Encoding.UTF8.GetString(payload).Split('\n', 3);
        if (parts.Length != 3
            || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
            || time.GetUtcNow().ToUnixTimeSeconds() >= expires)
        {
            return null;
        }

        var path = PathFor(parts[2]);
        return File.Exists(path) ? new StoredFile(File.OpenRead(path), parts[1]) : null;
    }

    private byte[] Sign(byte[] payload) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.SigningKey), payload);

    private string PathFor(string key)
    {
        var root = Path.GetFullPath(options.Value.RootPath);
        var path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Storage key escapes the storage root: {key}", nameof(key));
        }

        return path;
    }
}
