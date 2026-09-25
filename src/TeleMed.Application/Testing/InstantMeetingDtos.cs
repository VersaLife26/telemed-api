namespace TeleMed.Application.Testing;

public sealed record CreateInstantMeetingRequest(Guid? CounterpartUserId, string? CounterpartPhone, string? CounterpartEmail);

public sealed record InstantMeetingDto(Guid AppointmentId);
