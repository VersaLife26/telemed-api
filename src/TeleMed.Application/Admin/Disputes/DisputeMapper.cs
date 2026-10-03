using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Disputes;

internal static class DisputeMapper
{
    public static DisputeDto ToDto(this Dispute dispute) =>
        new(
            dispute.Id,
            dispute.AppointmentId,
            dispute.PatientId,
            dispute.DoctorId,
            dispute.Category,
            Opener(dispute),
            dispute.Subject,
            dispute.Description,
            dispute.Status,
            dispute.OpenedByAdminId,
            dispute.OpenedByUserId,
            dispute.AssignedAdminId,
            dispute.Resolution,
            dispute.ResolvedAt,
            dispute.ResolvedByAdminId,
            dispute.ClosedAt,
            dispute.CreatedAt,
            dispute.UpdatedAt);

    public static DisputeCommentDto ToDto(this DisputeComment comment) =>
        new(comment.Id, comment.AuthorAdminId, comment.AuthorUserId, comment.AuthorAdminId is not null, comment.Body, comment.CreatedAt);

    private static DisputeOpener Opener(Dispute dispute)
    {
        if (dispute.OpenedByAdminId is not null)
        {
            return DisputeOpener.Admin;
        }

        return dispute.OpenedByUserId is { } user && dispute.PatientId == user
            ? DisputeOpener.Patient
            : DisputeOpener.Doctor;
    }
}
