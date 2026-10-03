using System.Net;

namespace TeleMed.Application.Abstractions;

public interface IRequestContext
{
    IPAddress? IpAddress { get; }
    string? UserAgent { get; }
    string? RequestId { get; }
    // ISO 3166-1 alpha-2 from Cloudflare's CF-IPCountry header, or null when it is absent.
    string? CountryCode { get; }
}
