using System.Net;

namespace TeleMed.Application.Abstractions;

public interface IRequestContext
{
    IPAddress? IpAddress { get; }
    string? UserAgent { get; }
    string? RequestId { get; }
}
