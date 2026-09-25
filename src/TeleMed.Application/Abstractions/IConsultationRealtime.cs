using TeleMed.Application.Consultations;

namespace TeleMed.Application.Abstractions;

// Pushes to the peers connected to a consultation's signalling room. Call after commit.
public interface IConsultationRealtime
{
    Task StateChangedAsync(ConsultationDto state, CancellationToken ct);
    Task MessagePostedAsync(Guid consultationId, ConsultationMessageDto message, CancellationToken ct);
    Task ClosedAsync(Guid consultationId, string reason, CancellationToken ct);
}

public interface IConsultationPresence
{
    bool IsOccupied(Guid consultationId);
}
