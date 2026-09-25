using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Disputes;

public sealed record DisputeDto(
    Guid Id,
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    string Subject,
    string Description,
    DisputeStatus Status,
    Guid OpenedByAdminId,
    Guid? AssignedAdminId,
    string? Resolution,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedByAdminId,
    DateTimeOffset? ClosedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DisputeCommentDto(Guid Id, Guid AuthorAdminId, string Body, DateTimeOffset CreatedAt);

public sealed record DisputeDetailDto(DisputeDto Dispute, IReadOnlyList<DisputeCommentDto> Comments, IReadOnlyList<AdminRefundDto> Refunds);

public sealed record DisputeQuery : PageQuery
{
    public DisputeStatus? Status { get; init; }
    public Guid? AssignedAdminId { get; init; }
    public Guid? AppointmentId { get; init; }
}

public sealed record CreateDisputeRequest(Guid AppointmentId, string Subject, string Description);

public sealed record AddDisputeCommentRequest(string Body);

public sealed record AssignDisputeRequest(Guid? AdminUserId);

public sealed record ResolveDisputeRequest(string Resolution, long? RefundAmountCents);
