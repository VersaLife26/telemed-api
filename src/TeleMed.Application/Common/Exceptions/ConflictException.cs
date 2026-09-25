namespace TeleMed.Application.Common.Exceptions;

public sealed class ConflictException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    public string Code { get; } = code;

    public Dictionary<string, object?> Extensions { get; } = [];
}
