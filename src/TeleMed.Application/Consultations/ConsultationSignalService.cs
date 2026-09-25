using TeleMed.Application.Abstractions;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Consultations;

// The signalling hub's view of a consultation: whether its room may be entered, and the join/leave history.
public sealed class ConsultationSignalService(
    IConsultationRepository consultations,
    ITurnCredentialProvider turn,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public async Task<IReadOnlyList<IceServer>> OpenAsync(RoomGrant grant, CancellationToken ct)
    {
        if (await consultations.FindAsync(grant.ConsultationId, ct) is not { } consultation || ConsultationTransitions.IsTerminal(consultation.Status))
        {
            throw new ConflictException("room_closed", "This consultation has ended.");
        }

        return await turn.GetIceServersAsync(ct);
    }

    public Task PeerJoinedAsync(RoomGrant grant, CancellationToken ct) => RecordAsync(grant, ConsultationEventKind.Joined, ct);

    public Task PeerLeftAsync(RoomGrant grant, CancellationToken ct) => RecordAsync(grant, ConsultationEventKind.Left, ct);

    private async Task RecordAsync(RoomGrant grant, ConsultationEventKind kind, CancellationToken ct)
    {
        consultations.AddEvent(ConsultationService.Event(grant.ConsultationId, kind, grant.Role, null, time.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(ct);
    }
}
