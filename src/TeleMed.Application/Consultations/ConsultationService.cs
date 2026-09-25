using System.Text.Json;
using System.Text.Json.Serialization;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Notifications;
using TeleMed.Application.Payments;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Consultations;

public sealed class ConsultationService(
    ICurrentActor actor,
    IAppointmentRepository appointments,
    IDoctorRepository doctors,
    IConsultationRepository consultations,
    IRoomTokens roomTokens,
    ITurnCredentialProvider turn,
    IConsultationRealtime realtime,
    IUnitOfWork unitOfWork,
    PaymentLifecycle lifecycle,
    AppointmentNotifier notifier,
    TimeProvider time)
{
    public const string HubPath = "/hubs/consultation";
    public const string DowngradeHint = "audio_only";

    internal static readonly JsonSerializerOptions EventJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<JoinConsultationDto> JoinAsync(Guid appointmentId, CancellationToken ct)
    {
        var (appointment, role) = await ParticipantAsync(appointmentId, ct);
        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            throw new ConflictException("not_confirmed", "Only confirmed appointments can be joined.");
        }

        var now = time.GetUtcNow();
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var consultation = await consultations.LockOrCreateAsync(appointment.Id, now, ct);
        EnsureOpen(consultation);
        EnsureJoinWindow(role, appointment, consultation, now);

        var previous = consultation.Status;
        if (role == UserRole.Patient)
        {
            consultation.PatientJoinedAt ??= now;
            if (consultation.Status == ConsultationStatus.Scheduled)
            {
                consultation.Status = ConsultationStatus.Waiting;
                consultation.PatientWaitingSince = now;
            }
        }
        else
        {
            consultation.DoctorJoinedAt ??= now;
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        if (consultation.Status != previous)
        {
            await realtime.StateChangedAsync(consultation.ToDto(), ct);
        }

        var counterpartName = role == UserRole.Patient
            ? (await doctors.FindAsync(appointment.DoctorId, ct))?.DisplayName ?? ""
            : appointment.VisitPatientName;
        return new JoinConsultationDto(
            consultation.Id,
            role,
            consultation.Status,
            roomTokens.Issue(new RoomGrant(consultation.Id, actor.RequireUserId(), role)),
            HubPath,
            await turn.GetIceServersAsync(ct),
            counterpartName,
            appointment.StartAt,
            appointment.EndAt);
    }

    public async Task<ConsultationDto> GetAsync(Guid appointmentId, CancellationToken ct)
    {
        await ParticipantAsync(appointmentId, ct);
        return (await consultations.FindByAppointmentAsync(appointmentId, ct))?.ToDto() ?? ConsultationDto.NotStarted(appointmentId);
    }

    public async Task<WaitingRoomDto> GetWaitingRoomAsync(Guid appointmentId, CancellationToken ct)
    {
        var (appointment, _) = await ParticipantAsync(appointmentId, ct);
        if (await consultations.FindByAppointmentAsync(appointmentId, ct) is not { Status: ConsultationStatus.Waiting } consultation)
        {
            return new WaitingRoomDto(false, 0, 0, 0);
        }

        var ahead = await consultations.CountAheadAsync(appointment.DoctorId, consultation.Id, appointment.StartAt, ct);
        var durations = await consultations.ListRecentDurationsAsync(appointment.DoctorId, PlatformPolicy.WaitEstimateSampleSize, ct);
        var average = durations.Count > 0
            ? durations.Average()
            : ((await doctors.FindAsync(appointment.DoctorId, ct))?.SlotDurationMinutes ?? PlatformPolicy.DefaultSlotDurationMinutes) * 60.0;
        return new WaitingRoomDto(true, ahead + 1, ahead, (int)Math.Round(ahead * average));
    }

    public async Task<ConsultationDto> AdmitAsync(Guid appointmentId, CancellationToken ct)
    {
        var (appointment, role) = await ParticipantAsync(appointmentId, ct);
        if (role != UserRole.Doctor)
        {
            throw NotFound();
        }

        var consultation = await consultations.FindForUpdateByAppointmentAsync(appointment.Id, ct);
        if (consultation is not { Status: ConsultationStatus.Waiting } || appointment.Status != AppointmentStatus.Confirmed)
        {
            throw new ConflictException("not_waiting", "The patient is not in the waiting room.");
        }

        var now = time.GetUtcNow();
        consultation.Status = ConsultationStatus.Active;
        consultation.AdmittedAt = now;
        consultation.StartedAt ??= now;
        consultation.DoctorJoinedAt ??= now;
        consultations.AddEvent(Event(consultation.Id, ConsultationEventKind.Admitted, UserRole.Doctor, null, now));
        await unitOfWork.SaveChangesAsync(ct);

        var dto = consultation.ToDto();
        await realtime.StateChangedAsync(dto, ct);
        return dto;
    }

    // Only the doctor ending the call completes the appointment; a patient ending it leaves the outcome to the doctor or the sweeps.
    public async Task<ConsultationDto> EndAsync(Guid appointmentId, EndConsultationRequest? request, CancellationToken ct)
    {
        var (_, role) = await ParticipantAsync(appointmentId, ct);
        var consultation = await consultations.FindForUpdateByAppointmentAsync(appointmentId, ct) ?? throw NotJoined();
        EnsureOpen(consultation);

        var now = time.GetUtcNow();
        var reason = string.IsNullOrWhiteSpace(request?.Reason) ? PlatformPolicy.ConsultationEndedReason : request.Reason.Trim();
        consultation.End(ConsultationStatus.Ended, role, reason, now);
        consultations.AddEvent(Event(consultation.Id, ConsultationEventKind.Ended, role, new { reason }, now));
        if (role == UserRole.Doctor && await appointments.FindForUpdateAsync(appointmentId, ct) is { Status: AppointmentStatus.Confirmed } appointment)
        {
            lifecycle.Complete(appointment);
        }

        await unitOfWork.SaveChangesAsync(ct);

        var dto = consultation.ToDto();
        await realtime.StateChangedAsync(dto, ct);
        await realtime.ClosedAsync(consultation.Id, reason, ct);
        return dto;
    }

    public async Task<QualityFeedbackDto> ReportQualityAsync(Guid appointmentId, QualityReportRequest request, CancellationToken ct)
    {
        var (_, role) = await ParticipantAsync(appointmentId, ct);
        var consultation = await consultations.FindByAppointmentAsync(appointmentId, ct) ?? throw NotJoined();
        EnsureOpen(consultation);

        consultations.AddEvent(Event(consultation.Id, ConsultationEventKind.Quality, role, request, time.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(ct);

        var recent = await consultations.ListRecentEventDataAsync(consultation.Id, ConsultationEventKind.Quality, role, PlatformPolicy.PoorQualityStreak, ct);
        var downgrade = recent.Count == PlatformPolicy.PoorQualityStreak
            && recent.All(data => data is not null
                && JsonSerializer.Deserialize<QualityReportRequest>(data, EventJson)?.Quality is CallQuality.Poor or CallQuality.Lost);
        return new QualityFeedbackDto(downgrade, downgrade ? DowngradeHint : null);
    }

    public async Task<PagedResult<ConsultationMessageDto>> ListMessagesAsync(Guid appointmentId, ConsultationMessageQuery query, CancellationToken ct)
    {
        await ParticipantAsync(appointmentId, ct);
        if (await consultations.FindByAppointmentAsync(appointmentId, ct) is not { } consultation)
        {
            return new PagedResult<ConsultationMessageDto>([], query.Page, query.PageSize, 0);
        }

        var (items, total) = await consultations.ListMessagesAsync(consultation.Id, query.Skip, query.PageSize, ct);
        return new PagedResult<ConsultationMessageDto>(items.Select(m => m.ToDto()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<ConsultationMessageDto> PostMessageAsync(Guid appointmentId, PostMessageRequest request, CancellationToken ct)
    {
        var (_, role) = await ParticipantAsync(appointmentId, ct);
        var consultation = await consultations.FindByAppointmentAsync(appointmentId, ct) ?? throw NotJoined();
        EnsureOpen(consultation);

        var message = new ConsultationMessage
        {
            ConsultationId = consultation.Id,
            SenderUserId = actor.RequireUserId(),
            SenderRole = role,
            Body = request.Body.Trim(),
        };
        consultations.AddMessage(message);
        await unitOfWork.SaveChangesAsync(ct);

        var dto = message.ToDto();
        await realtime.MessagePostedAsync(consultation.Id, dto, ct);
        return dto;
    }

    // Offers the doctor's next confirmed patient today the chance to start now instead of waiting for their slot.
    public async Task<ReadyForNextDto> ReadyForNextAsync(Guid appointmentId, CancellationToken ct)
    {
        var (current, role) = await ParticipantAsync(appointmentId, ct);
        if (role != UserRole.Doctor)
        {
            throw NotFound();
        }

        var finished = current.Status == AppointmentStatus.Completed
            || await consultations.FindByAppointmentAsync(current.Id, ct) is { Status: ConsultationStatus.Active or ConsultationStatus.Ended };
        if (!finished)
        {
            throw new ConflictException("not_in_consultation", "Ready-for-next applies to a consultation that is in progress or finished.");
        }

        var doctor = await doctors.FindAsync(current.DoctorId, ct) ?? throw NotFound();
        var now = time.GetUtcNow();
        var zone = IanaTimeZone.Find(doctor.TimeZone);
        var tomorrow = SlotPlanner.LocalDate(now, zone).AddDays(1).ToDateTime(TimeOnly.MinValue);
        var endOfDay = new DateTimeOffset(tomorrow, zone.GetUtcOffset(tomorrow)).ToUniversalTime();
        var next = await appointments.FindNextConfirmedAsync(
            doctor.Id, current.Id, current.StartAt > now ? current.StartAt : now, endOfDay, ct);
        if (next is null)
        {
            return new ReadyForNextDto(ReadyForNextStatus.NoNextAppointment, null, null, null, null);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var consultation = await consultations.LockOrCreateAsync(next.Id, now, ct);
        ReadyForNextStatus status;
        if (consultation.Status is ConsultationStatus.Waiting or ConsultationStatus.Active)
        {
            status = ReadyForNextStatus.AlreadyWaiting;
        }
        else if (consultation.EarlyJoinOfferedAt is not null)
        {
            status = consultation.EarlyJoinResponse == EarlyJoinResponse.Declined ? ReadyForNextStatus.Declined : ReadyForNextStatus.AlreadyOffered;
        }
        else
        {
            consultation.EarlyJoinOfferedAt = now;
            await notifier.EarlyJoinOfferedAsync(next, doctor, ct);
            status = ReadyForNextStatus.Offered;
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new ReadyForNextDto(status, next.Id, next.StartAt, consultation.EarlyJoinOfferedAt, consultation.EarlyJoinResponse);
    }

    public async Task<EarlyJoinDto> GetEarlyJoinAsync(Guid appointmentId, CancellationToken ct)
    {
        var (appointment, _) = await ParticipantAsync(appointmentId, ct);
        var consultation = await consultations.FindByAppointmentAsync(appointmentId, ct);
        return ToEarlyJoinDto(appointment, consultation);
    }

    public async Task<EarlyJoinDto> RespondEarlyJoinAsync(Guid appointmentId, EarlyJoinResponse response, CancellationToken ct)
    {
        var (appointment, role) = await ParticipantAsync(appointmentId, ct);
        if (role != UserRole.Patient)
        {
            throw NotFound();
        }

        var consultation = await consultations.FindForUpdateByAppointmentAsync(appointmentId, ct);
        var offer = ToEarlyJoinDto(appointment, consultation);
        if (consultation!.EarlyJoinResponse is { } existing)
        {
            return existing == response
                ? offer
                : throw new ConflictException("early_join_answered", "You have already answered this early-join offer.");
        }

        if (appointment.Status != AppointmentStatus.Confirmed || ConsultationTransitions.IsTerminal(consultation.Status))
        {
            throw new ConflictException("early_join_unavailable", "This early-join offer is no longer available.");
        }

        consultation.EarlyJoinResponse = response;
        consultation.EarlyJoinRespondedAt = time.GetUtcNow();
        await unitOfWork.SaveChangesAsync(ct);
        return ToEarlyJoinDto(appointment, consultation);
    }

    internal static ConsultationEvent Event(Guid consultationId, ConsultationEventKind kind, UserRole? role, object? data, DateTimeOffset at) => new()
    {
        ConsultationId = consultationId,
        Kind = kind,
        ActorRole = role,
        Data = data is null ? null : JsonSerializer.Serialize(data, EventJson),
        CreatedAt = at,
    };

    // An accepted early-join opens the room for both sides; after that the booked times apply. Rejoining a live call is always allowed.
    private static void EnsureJoinWindow(UserRole role, Appointment appointment, Consultation consultation, DateTimeOffset now)
    {
        if (consultation.Status == ConsultationStatus.Active)
        {
            return;
        }

        var opensAt = consultation.EarlyJoinResponse == EarlyJoinResponse.Accepted
            ? DateTimeOffset.MinValue
            : appointment.StartAt - PlatformPolicy.JoinOpensBefore;
        var closesAt = role == UserRole.Patient ? appointment.EndAt : appointment.EndAt + PlatformPolicy.DoctorJoinGraceAfterEnd;
        if (now < opensAt)
        {
            throw new ConflictException("too_early", "The consultation room is not open yet.") { Extensions = { ["opensAt"] = opensAt } };
        }

        if (now >= closesAt)
        {
            throw new ConflictException("join_window_closed", "The time to join this consultation has passed.");
        }
    }

    private static void EnsureOpen(Consultation consultation)
    {
        if (ConsultationTransitions.IsTerminal(consultation.Status))
        {
            throw new ConflictException("consultation_ended", "This consultation has ended.");
        }
    }

    private static EarlyJoinDto ToEarlyJoinDto(Appointment appointment, Consultation? consultation) =>
        consultation is { EarlyJoinOfferedAt: { } offeredAt }
            ? new EarlyJoinDto(appointment.Id, appointment.StartAt, offeredAt, consultation.EarlyJoinResponse, consultation.EarlyJoinRespondedAt)
            : throw new NotFoundException("There is no early-join offer for this appointment.");

    // Anyone but the two participants gets a 404 so appointment ids cannot be probed.
    private async Task<(Appointment Appointment, UserRole Role)> ParticipantAsync(Guid appointmentId, CancellationToken ct)
    {
        var userId = actor.RequireUserId();
        var appointment = await appointments.FindAsync(appointmentId, ct) ?? throw NotFound();
        if (actor.Role == UserRole.Patient && appointment.PatientId == userId)
        {
            return (appointment, UserRole.Patient);
        }

        if (actor.Role == UserRole.Doctor && await doctors.FindByUserIdAsync(userId, ct) is { } doctor && doctor.Id == appointment.DoctorId)
        {
            return (appointment, UserRole.Doctor);
        }

        throw NotFound();
    }

    private static NotFoundException NotFound() => new("Appointment not found.");

    private static ConflictException NotJoined() => new("not_joined", "Nobody has joined this consultation yet.");
}
