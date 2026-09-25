using TeleMed.Domain.Rules;

namespace TeleMed.Application.Users;

internal static class ProfilePhoto
{
    public static string ContentType(string storageKey) =>
        FileSignature.ContentTypeFromExtension(Path.GetExtension(storageKey)) ?? "application/octet-stream";
}
