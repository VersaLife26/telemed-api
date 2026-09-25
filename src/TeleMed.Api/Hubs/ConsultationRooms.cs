using Microsoft.AspNetCore.SignalR;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.Hubs;

// In-memory, so presence is per instance: the hub assumes a single API instance.
public sealed class ConsultationRooms : IConsultationPresence
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, Dictionary<UserRole, HubCallerContext>> _rooms = [];

    public (HubCallerContext? Replaced, HubCallerContext? Peer) Join(Guid consultationId, UserRole role, HubCallerContext connection)
    {
        lock (_gate)
        {
            if (!_rooms.TryGetValue(consultationId, out var room))
            {
                _rooms[consultationId] = room = [];
            }

            room.Remove(role, out var replaced);
            room[role] = connection;
            return (replaced, room.GetValueOrDefault(Other(role)));
        }
    }

    // False when this connection had already been replaced, so its departure is not the role leaving.
    public (bool Left, HubCallerContext? Peer) Leave(Guid consultationId, UserRole role, string connectionId)
    {
        lock (_gate)
        {
            if (!_rooms.TryGetValue(consultationId, out var room)
                || !room.TryGetValue(role, out var current)
                || current.ConnectionId != connectionId)
            {
                return (false, null);
            }

            room.Remove(role);
            if (room.Count == 0)
            {
                _rooms.Remove(consultationId);
            }

            return (true, room.GetValueOrDefault(Other(role)));
        }
    }

    public HubCallerContext? Peer(Guid consultationId, UserRole role)
    {
        lock (_gate)
        {
            return _rooms.TryGetValue(consultationId, out var room) ? room.GetValueOrDefault(Other(role)) : null;
        }
    }

    public bool IsOccupied(Guid consultationId)
    {
        lock (_gate)
        {
            return _rooms.ContainsKey(consultationId);
        }
    }

    private static UserRole Other(UserRole role) => role == UserRole.Patient ? UserRole.Doctor : UserRole.Patient;
}
