using Microsoft.AspNetCore.SignalR;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Consultations;

namespace TeleMed.Api.Hubs;

internal sealed class ConsultationRealtime(IHubContext<ConsultationHub> hub) : IConsultationRealtime
{
    public Task StateChangedAsync(ConsultationDto state, CancellationToken ct) =>
        SendAsync(state.Id!.Value, new SignalFrame(SignalTypes.StateChanged, Data: state), ct);

    public Task MessagePostedAsync(Guid consultationId, ConsultationMessageDto message, CancellationToken ct) =>
        SendAsync(consultationId, new SignalFrame(SignalTypes.Chat, Data: message), ct);

    public Task ClosedAsync(Guid consultationId, string reason, CancellationToken ct) =>
        SendAsync(consultationId, new SignalFrame(SignalTypes.RoomClosed, Data: new { reason }), ct);

    private Task SendAsync(Guid consultationId, SignalFrame frame, CancellationToken ct) =>
        hub.Clients.Group(ConsultationHub.Group(consultationId)).SendAsync(ConsultationHub.FrameMethod, frame, ct);
}
