using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TeleMed.Application.Consultations;
using TeleMed.Application.Testing;

namespace TeleMed.Api.IntegrationTests.Infrastructure;

public static class ConsultationFlows
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string Url(Guid appointmentId, string path = "") => $"/api/v1/appointments/{appointmentId}/consultation{path}";

    public static Task<HttpResponseMessage> JoinAsync(this HttpClient client, Guid appointmentId) =>
        client.PostAsync(Url(appointmentId, "/join"), null, Ct);

    public static async Task<JoinConsultationDto> JoinedAsync(this HttpClient client, Guid appointmentId) =>
        await (await client.JoinAsync(appointmentId)).ReadAsync<JoinConsultationDto>();

    public static async Task<ConsultationDto> ConsultationAsync(this HttpClient client, Guid appointmentId) =>
        await (await client.GetAsync(Url(appointmentId), Ct)).ReadAsync<ConsultationDto>();

    public static async Task<ConsultationDto> AdmittedAsync(this HttpClient doctor, Guid appointmentId) =>
        await (await doctor.PostAsync(Url(appointmentId, "/admit"), null, Ct)).ReadAsync<ConsultationDto>();

    public static async Task<ConsultationDto> EndedAsync(this HttpClient client, Guid appointmentId, string? reason = null) =>
        await (await client.PostJsonAsync(Url(appointmentId, "/end"), new { reason })).ReadAsync<ConsultationDto>();

    public static Task<HttpResponseMessage> CreateInstantMeetingAsync(this HttpClient caller, object body)
    {
        caller.DefaultRequestHeaders.Remove("X-Test-Secret");
        caller.DefaultRequestHeaders.Add("X-Test-Secret", TeleMedApiFactory.TestSecret);
        return caller.PostJsonAsync("/api/v1/test/instant-meetings", body);
    }

    public static async Task<Guid> InstantMeetingAsync(this HttpClient caller, object body) =>
        (await (await caller.CreateInstantMeetingAsync(body)).ReadAsync<InstantMeetingDto>(HttpStatusCode.Created)).AppointmentId;

    public static async Task<Peer> ConnectAsync(this TeleMedApiFactory factory, string roomToken)
    {
        var frames = Channel.CreateUnbounded<JsonElement>();
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, $"{ConsultationService.HubPath.TrimStart('/')}?roomToken={Uri.EscapeDataString(roomToken)}"), o =>
            {
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        connection.On<JsonElement>("frame", frame => frames.Writer.TryWrite(frame));
        connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };
        await connection.StartAsync(Ct);
        return new Peer(connection, frames.Reader, closed.Task);
    }

    public sealed class Peer(HubConnection connection, ChannelReader<JsonElement> frames, Task closed) : IAsyncDisposable
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

        public HubConnection Connection => connection;

        public Task Closed => closed;

        public Task SendAsync(object frame) => connection.InvokeAsync("Send", frame, Ct);

        // Skips frames of other types, so tests only assert on what they are waiting for.
        public async Task<JsonElement> NextAsync(string type)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(Wait);
            while (true)
            {
                var frame = await frames.ReadAsync(timeout.Token);
                if (frame.GetProperty("type").GetString() == type)
                {
                    return frame;
                }
            }
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
