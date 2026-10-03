using TeleMed.Application.Abstractions;
using TeleMed.Application.Admin.AdminUsers;
using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.CustomerCare;
using TeleMed.Application.Notifications;
using TeleMed.Application.Payments;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Disputes;

// open -> investigating (on assignment) -> resolved -> closed. A refund decided on resolution is only requested here;
// finance still approves it like any other refund request.
public sealed class DisputeService(
    ICurrentActor actor,
    IDisputeRepository disputes,
    IAdminUserRepository admins,
    IPaymentRepository payments,
    IFinanceRepository finance,
    PaymentLifecycle lifecycle,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public async Task<PagedResult<DisputeDto>> ListAsync(DisputeQuery query, CancellationToken ct)
    {
        var (items, total) = await disputes.ListAsync(query.Status, query.Category, query.AssignedAdminId, query.AppointmentId, query.Skip, query.PageSize, ct);
        return new PagedResult<DisputeDto>(items.Select(d => d.ToDto()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<DisputeDetailDto> GetAsync(Guid id, CancellationToken ct) =>
        await DetailAsync(await disputes.FindAsync(id, ct) ?? throw NotFound(), ct);

    public async Task<DisputeCommentDto> AddCommentAsync(Guid id, AddDisputeCommentRequest request, CancellationToken ct)
    {
        var admin = actor.RequireAdmin();
        var dispute = await disputes.FindForUpdateAsync(id, ct) ?? throw NotFound();
        if (dispute.Status == DisputeStatus.Closed)
        {
            throw new ConflictException("conversation_closed", "This conversation is closed.");
        }

        var comment = new DisputeComment { DisputeId = id, AuthorAdminId = admin.Id, Body = request.Body.Trim() };
        disputes.AddComment(comment);
        dispute.UpdatedAt = time.GetUtcNow();
        await CustomerCareService.NotifyOpenerAsync(notifications, dispute, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return comment.ToDto();
    }

    public async Task<DisputeDetailDto> AssignAsync(Guid id, AssignDisputeRequest request, CancellationToken ct)
    {
        var admin = actor.RequireAdmin();
        var dispute = await LoadAsync(id, ct);
        EnsureStatus(dispute, DisputeStatus.Open, DisputeStatus.Investigating);
        var assignee = request.AdminUserId ?? admin.Id;
        if (await admins.FindAsync(assignee, ct) is not { IsActive: true })
        {
            throw new BadRequestException("invalid_assignee", "The assignee must be an active admin user.");
        }

        dispute.AssignedAdminId = assignee;
        dispute.Status = DisputeStatus.Investigating;
        await unitOfWork.SaveChangesAsync(ct);
        return await DetailAsync(dispute, ct);
    }

    public async Task<DisputeDetailDto> ResolveAsync(Guid id, ResolveDisputeRequest request, CancellationToken ct)
    {
        var admin = actor.RequireAdmin();
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var dispute = await LoadAsync(id, ct);
        EnsureStatus(dispute, DisputeStatus.Open, DisputeStatus.Investigating);
        if (request.RefundAmountCents is { } amount)
        {
            if (dispute.AppointmentId is not { } appointmentId)
            {
                throw new ConflictException("payment_not_refundable", "This conversation is not tied to an appointment, so a refund cannot be requested here.");
            }

            var snapshot = await payments.FindByAppointmentAsync(appointmentId, ct)
                ?? throw new ConflictException("payment_not_refundable", "This appointment has no payment to refund.");
            var payment = (await payments.LockAsync(snapshot.Id, ct))!;
            await lifecycle.StageRequestedRefundAsync(payment, amount, RefundReason.Dispute, request.Resolution.Trim(), admin.Id, dispute.Id, ct);
        }

        dispute.Status = DisputeStatus.Resolved;
        dispute.Resolution = request.Resolution.Trim();
        dispute.ResolvedAt = time.GetUtcNow();
        dispute.ResolvedByAdminId = admin.Id;
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await DetailAsync(dispute, ct);
    }

    public async Task<DisputeDetailDto> CloseAsync(Guid id, CancellationToken ct)
    {
        actor.RequireAdmin();
        var dispute = await LoadAsync(id, ct);
        EnsureStatus(dispute, DisputeStatus.Resolved);
        dispute.Status = DisputeStatus.Closed;
        dispute.ClosedAt = time.GetUtcNow();
        await unitOfWork.SaveChangesAsync(ct);
        return await DetailAsync(dispute, ct);
    }

    private async Task<DisputeDetailDto> DetailAsync(Dispute dispute, CancellationToken ct) =>
        new(
            dispute.ToDto(),
            (await disputes.ListCommentsAsync(dispute.Id, ct)).Select(c => c.ToDto()).ToList(),
            await finance.ListRefundsForDisputeAsync(dispute.Id, ct));

    private async Task<Dispute> LoadAsync(Guid id, CancellationToken ct) => await disputes.FindForUpdateAsync(id, ct) ?? throw NotFound();

    private static void EnsureStatus(Dispute dispute, params DisputeStatus[] allowed)
    {
        if (!allowed.Contains(dispute.Status))
        {
            throw new ConflictException("invalid_transition", $"A {dispute.Status} case cannot take this action.");
        }
    }

    private static NotFoundException NotFound() => new("Customer care case not found.");
}
