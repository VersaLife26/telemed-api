namespace TeleMed.Application.Abstractions;

public interface IFileStorage
{
    TimeSpan SignedUrlLifetime { get; }
    Task SaveAsync(string key, Stream content, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
    Stream? OpenRead(string key);
    string CreateSignedUrl(string key, string contentType);
    StoredFile? OpenSigned(string token);
}

public sealed record StoredFile(Stream Content, string ContentType);
