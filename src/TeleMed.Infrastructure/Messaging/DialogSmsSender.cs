using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Messaging;

internal sealed class DialogSmsSender(HttpClient http, IOptions<SmsOptions> options) : ISmsSender
{
    public bool IsEnabled => true;

    public async Task SendAsync(string phoneNumber, string message, CancellationToken ct)
    {
        var dialog = options.Value.Dialog;
        using var response = await http.PostAsJsonAsync(dialog.BaseUrl, new DialogSendRequest(
            dialog.ApplicationId,
            dialog.Password,
            message,
            [phoneNumber],
            dialog.SourceAddress), ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Dialog SMS failed with {(int)response.StatusCode}: {body[..Math.Min(body.Length, 200)]}", null, response.StatusCode);
        }
    }

    private sealed record DialogSendRequest(
        string ApplicationId,
        string Password,
        string Message,
        string[] DestinationAddresses,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SourceAddress);
}
