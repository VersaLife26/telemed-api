namespace TeleMed.Application.Common.Exceptions;

public sealed class ForbiddenException(string message, string? code = null) : Exception(message)
{
    public string? Code { get; } = code;
}
