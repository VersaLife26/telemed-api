using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Consultations;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Hubs;

// Two-peer WebRTC signalling. Authenticated by the room token from POST .../consultation/join, not by the access token.
[AllowAnonymous]
public sealed class ConsultationHub(IRoomTokens roomTokens, ConsultationSignalService signalling, ConsultationRooms rooms) : Hub
{
    public const string FrameMethod = "frame";
    public const string RoomTokenQuery = "roomToken";

    private const string GrantKey = "grant";

    public static string Group(Guid consultationId) => $"consultation:{consultationId:N}";

    public override async Task OnConnectedAsync()
    {
        var grant = roomTokens.Validate(Context.GetHttpContext()?.Request.Query[RoomTokenQuery])
            ?? throw new HubException("The room token is missing, invalid or expired.");
        IReadOnlyList<IceServer> iceServers;
        try
        {
            iceServers = await signalling.OpenAsync(grant, Context.ConnectionAborted);
        }
        catch (ConflictException ex)
        {
            await Clients.Caller.SendAsync(FrameMethod, new SignalFrame(SignalTypes.RoomClosed, Data: new { reason = ex.Code }));
            Context.Abort();
            return;
        }

        Context.Items[GrantKey] = grant;
        var (replaced, peer) = rooms.Join(grant.ConsultationId, grant.Role, Context);
        if (replaced is not null)
        {
            await Clients.Client(replaced.ConnectionId).SendAsync(FrameMethod, new SignalFrame(SignalTypes.RoomClosed, Data: new { reason = "replaced" }));
            replaced.Abort();
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, Group(grant.ConsultationId));
        await Clients.Caller.SendAsync(
            FrameMethod,
            new SignalFrame(
                SignalTypes.Welcome,
                Data: new WelcomeData(Context.ConnectionId, grant.Role, grant.Role == UserRole.Patient, peer is not null, iceServers)));
        if (peer is not null)
        {
            await Clients.Client(peer.ConnectionId).SendAsync(FrameMethod, new SignalFrame(SignalTypes.PeerJoined, Context.ConnectionId));
        }

        await signalling.PeerJoinedAsync(grant, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items[GrantKey] is not RoomGrant grant)
        {
            return;
        }

        var (left, peer) = rooms.Leave(grant.ConsultationId, grant.Role, Context.ConnectionId);
        if (!left)
        {
            return;
        }

        if (peer is not null)
        {
            await Clients.Client(peer.ConnectionId).SendAsync(FrameMethod, new SignalFrame(SignalTypes.PeerLeft, Context.ConnectionId));
        }

        await signalling.PeerLeftAsync(grant, CancellationToken.None);
    }

    public async Task Send(SignalFrame? frame)
    {
        if (Context.Items[GrantKey] is not RoomGrant grant)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(frame?.Type))
        {
            await ErrorAsync(SignalErrorCodes.Malformed, "A frame needs a type.");
        }
        else if (frame.Type == SignalTypes.Ping)
        {
            await Clients.Caller.SendAsync(FrameMethod, new SignalFrame(SignalTypes.Pong));
        }
        else if (!SignalTypes.Relayable.Contains(frame.Type))
        {
            await ErrorAsync(SignalErrorCodes.UnknownType, $"Frames of type '{frame.Type}' are not relayed.");
        }
        else if (rooms.Peer(grant.ConsultationId, grant.Role) is not { } peer)
        {
            await ErrorAsync(SignalErrorCodes.NoPeer, "The other participant is not connected.");
        }
        else
        {
            await Clients.Client(peer.ConnectionId).SendAsync(FrameMethod, frame with { From = Context.ConnectionId });
        }
    }

    private Task ErrorAsync(string code, string message) =>
        Clients.Caller.SendAsync(FrameMethod, new SignalFrame(SignalTypes.Error, Data: new SignalError(code, message)));
}
