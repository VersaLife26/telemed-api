using System.Net;
using TeleMed.Application.Abstractions;

namespace TeleMed.Api.Auth;

internal sealed class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public IPAddress? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress is { } ip ? ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip : null;

    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent is { Count: > 0 } ua ? ua.ToString() : null;

    public string? RequestId => accessor.HttpContext?.TraceIdentifier;
}
