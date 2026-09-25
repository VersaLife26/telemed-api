using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Consultations;

public sealed record ConsultationDto(
    Guid? Id,
    Guid AppointmentId,
    ConsultationStatus Status,
    DateTimeOffset? PatientWaitingSince,
    DateTimeOffset? AdmittedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    UserRole? EndedByRole,
    string? EndReason,
    int? DurationSeconds,
    DateTimeOffset? DoctorJoinedAt,
    DateTimeOffset? PatientJoinedAt)
{
    public static ConsultationDto NotStarted(Guid appointmentId) =>
        new(null, appointmentId, ConsultationStatus.Scheduled, null, null, null, null, null, null, null, null, null);
}

public sealed record JoinConsultationDto(
    Guid ConsultationId,
    UserRole Role,
    ConsultationStatus Status,
    string RoomToken,
    string HubUrl,
    IReadOnlyList<IceServer> IceServers,
    string CounterpartName,
    DateTimeOffset ScheduledStartAt,
    DateTimeOffset ScheduledEndAt);

public sealed record WaitingRoomDto(bool Waiting, int Position, int PatientsAhead, int EstimatedWaitSeconds);

public sealed record EndConsultationRequest(string? Reason);

public sealed record QualityReportRequest(CallQuality Quality, decimal? PacketLossPct, int? BitrateKbps);

public sealed record QualityFeedbackDto(bool Downgrade, string? Hint);

public sealed record PostMessageRequest(string Body);

public sealed record ConsultationMessageDto(Guid Id, Guid ConsultationId, Guid SenderUserId, UserRole SenderRole, string Body, DateTimeOffset CreatedAt);

public sealed record ConsultationMessageQuery : PageQuery;

public enum ReadyForNextStatus
{
    Offered,
    AlreadyOffered,
    AlreadyWaiting,
    Declined,
    NoNextAppointment,
}

public sealed record ReadyForNextDto(
    ReadyForNextStatus Status, Guid? AppointmentId, DateTimeOffset? ScheduledStartAt, DateTimeOffset? OfferedAt, EarlyJoinResponse? Response);

public sealed record EarlyJoinDto(
    Guid AppointmentId, DateTimeOffset ScheduledStartAt, DateTimeOffset OfferedAt, EarlyJoinResponse? Response, DateTimeOffset? RespondedAt);

public sealed record UnattendedAppointment(Guid AppointmentId, Guid DoctorId);

public sealed record OverrunConsultation(Guid ConsultationId, Guid AppointmentId, Guid DoctorId, DateTimeOffset StartAt);

public sealed record IdleConsultation(Guid ConsultationId, Guid AppointmentId);
