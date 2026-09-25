namespace TeleMed.Application.Abstractions;

public interface ISmsSender
{
    bool IsEnabled { get; }
    Task SendAsync(string phoneNumber, string message, CancellationToken ct);
}
