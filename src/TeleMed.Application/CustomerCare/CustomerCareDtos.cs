using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.CustomerCare;

public sealed record CustomerCareSummaryDto(
    Guid Id,
    DisputeCategory Category,
    DisputeStatus Status,
    Guid? AppointmentId,
    string Subject,
    string LastMessage,
    DateTimeOffset UpdatedAt);

public sealed record CustomerCareMessageDto(Guid Id, string Body, bool FromSupport, DateTimeOffset CreatedAt);

public sealed record CustomerCareThreadDto(
    Guid Id,
    DisputeCategory Category,
    DisputeStatus Status,
    Guid? AppointmentId,
    string Subject,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CustomerCareMessageDto> Messages);

public sealed record OpenCustomerCareRequest(DisputeCategory Category, string Body, Guid? AppointmentId);

public sealed record CustomerCareMessageRequest(string Body);

public sealed record CustomerCareQuery : PageQuery;
