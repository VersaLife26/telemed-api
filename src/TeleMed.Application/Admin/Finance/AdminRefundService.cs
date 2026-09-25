using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Payments;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Finance;

// Approval hands the refund to PaymentSettlementJob, which sends it to the provider. A refund the provider cannot send
// (manual_required) is done by finance in the provider's portal and then marked refunded here.
public sealed class AdminRefundService(
    ICurrentActor actor,
    IFinanceRepository finance,
    IPaymentRepository payments,
    PaymentLifecycle lifecycle,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public async Task<PagedResult<AdminRefundDto>> ListAsync(AdminRefundQuery query, CancellationToken ct)
    {
        var (items, total) = await finance.ListRefundsAsync(query.Status, query.Skip, query.PageSize, ct);
        return new PagedResult<AdminRefundDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<AdminRefundDto> CreateAsync(Guid paymentId, CreateRefundRequest request, CancellationToken ct)
    {
        var admin = actor.RequireAdmin();
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var payment = await payments.LockAsync(paymentId, ct) ?? throw new NotFoundException("Payment not found.");
        var refund = await lifecycle.StageRequestedRefundAsync(payment, request.AmountCents, RefundReason.AdminRequest, request.Reason.Trim(), admin.Id, null, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await DtoAsync(refund.Id, ct);
    }

    public Task<AdminRefundDto> ApproveAsync(Guid id, CancellationToken ct) =>
        DecideAsync(id, RefundStatus.Requested, (refund, _) => refund.Status = RefundStatus.Processing, ct);

    public Task<AdminRefundDto> RejectAsync(Guid id, RejectRefundRequest request, CancellationToken ct) =>
        DecideAsync(id, RefundStatus.Requested, (refund, _) =>
        {
            refund.Status = RefundStatus.Rejected;
            refund.RejectionReason = request.Reason.Trim();
        }, ct);

    public Task<AdminRefundDto> MarkRefundedAsync(Guid id, MarkRefundedRequest request, CancellationToken ct) =>
        DecideAsync(id, RefundStatus.ManualRequired, (refund, payment) =>
        {
            refund.ProcessedAt = time.GetUtcNow();
            lifecycle.ApplyRefundSucceeded(payment, refund, request.Reference.Trim());
        }, ct);

    private async Task<AdminRefundDto> DecideAsync(Guid id, RefundStatus expected, Action<Refund, Payment> apply, CancellationToken ct)
    {
        var admin = actor.RequireAdmin();
        var snapshot = await finance.FindRefundAsync(id, ct) ?? throw NotFound();

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var payment = await payments.LockAsync(snapshot.PaymentId, ct) ?? throw NotFound();
        var refund = await payments.FindRefundForUpdateAsync(id, ct) ?? throw NotFound();
        if (refund.Status != expected)
        {
            throw new ConflictException("invalid_refund_status", $"This action needs a refund in status '{expected}', but it is '{refund.Status}'.");
        }

        refund.ReviewedByAdminId = admin.Id;
        refund.ReviewedAt = time.GetUtcNow();
        apply(refund, payment);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await DtoAsync(id, ct);
    }

    private async Task<AdminRefundDto> DtoAsync(Guid id, CancellationToken ct) => await finance.FindRefundAsync(id, ct) ?? throw NotFound();

    private static NotFoundException NotFound() => new("Refund not found.");
}
