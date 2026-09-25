namespace TeleMed.Application.Common.Exceptions;

public sealed class TooManyRequestsException(string message) : Exception(message);
