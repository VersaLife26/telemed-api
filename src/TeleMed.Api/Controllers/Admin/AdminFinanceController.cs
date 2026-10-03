using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TeleMed.Api.Auth;
using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Common;
using TeleMed.Application.Payouts;
using TeleMed.Application.Permissions;

namespace TeleMed.Api.Controllers.Admin;

[ApiController]
[Route("admin")]
[AdminAuthorize(AdminPermission.Finance)]
[EnableCors(CorsSetup.AdminPolicy)]
public sealed class AdminFinanceController(
    FinanceService finance,
    PayoutService payouts,
    AdminRefundService refunds,
    PromoCodeService promoCodes) : ControllerBase
{
    [HttpGet("finance/ledger")]
    public Task<LedgerPageDto> Ledger([FromQuery] LedgerQuery query, CancellationToken ct) => finance.ListLedgerAsync(query, ct);

    [HttpGet("finance/ledger.csv")]
    [Produces(Csv.ContentType)]
    public async Task LedgerCsv([FromQuery] LedgerExportQuery query, CancellationToken ct)
    {
        Response.ContentType = $"{Csv.ContentType}; charset=utf-8";
        Response.Headers.ContentDisposition = "attachment; filename=\"ledger.csv\"";
        await finance.WriteLedgerCsvAsync(query, Response.Body, ct);
    }

    [HttpGet("finance/commission")]
    public Task<CommissionDto> Commission(CancellationToken ct) => finance.GetCommissionAsync(ct);

    [HttpPut("finance/commission")]
    public Task<CommissionDto> UpdateCommission(UpdateCommissionRequest request, CancellationToken ct) =>
        finance.UpdateDefaultAsync(request, ct);

    [HttpPut("finance/commission/doctors/{doctorId:guid}")]
    public Task<CommissionDto> SetDoctorCommission(Guid doctorId, SetDoctorCommissionRequest request, CancellationToken ct) =>
        finance.SetDoctorRateAsync(doctorId, request, ct);

    [HttpGet("finance/payout-batches")]
    public Task<PagedResult<PayoutBatchDto>> ListPayoutBatches([FromQuery] PayoutBatchQuery query, CancellationToken ct) => payouts.ListBatchesAsync(query, ct);

    [HttpGet("finance/payout-batches/{id:guid}")]
    public Task<PayoutBatchDetailDto> GetPayoutBatch(Guid id, CancellationToken ct) => payouts.GetBatchAsync(id, ct);

    [HttpPost("finance/payouts/run")]
    public Task<PayoutRunDto> RunPayouts([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RunPayoutsRequest? request, CancellationToken ct) =>
        payouts.RunForAdminAsync(request ?? new RunPayoutsRequest(null), ct);

    [HttpPost("finance/payouts/{id:guid}/mark-paid")]
    public Task<PayoutDto> MarkPayoutPaid(Guid id, MarkPayoutPaidRequest request, CancellationToken ct) => payouts.MarkPaidAsync(id, request, ct);

    [HttpPost("finance/payouts/{id:guid}/mark-failed")]
    public Task<PayoutDto> MarkPayoutFailed(Guid id, MarkPayoutFailedRequest request, CancellationToken ct) => payouts.MarkFailedAsync(id, request, ct);

    [HttpGet("finance/refunds")]
    public Task<PagedResult<AdminRefundDto>> ListRefunds([FromQuery] AdminRefundQuery query, CancellationToken ct) => refunds.ListAsync(query, ct);

    [HttpPost("finance/refunds/{id:guid}/approve")]
    public Task<AdminRefundDto> ApproveRefund(Guid id, CancellationToken ct) => refunds.ApproveAsync(id, ct);

    [HttpPost("finance/refunds/{id:guid}/reject")]
    public Task<AdminRefundDto> RejectRefund(Guid id, RejectRefundRequest request, CancellationToken ct) => refunds.RejectAsync(id, request, ct);

    [HttpPost("finance/refunds/{id:guid}/mark-refunded")]
    public Task<AdminRefundDto> MarkRefunded(Guid id, MarkRefundedRequest request, CancellationToken ct) => refunds.MarkRefundedAsync(id, request, ct);

    [HttpPost("payments/{id:guid}/refunds")]
    [ProducesResponseType<AdminRefundDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> CreateRefund(Guid id, CreateRefundRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await refunds.CreateAsync(id, request, ct));

    [HttpGet("finance/promo-codes")]
    public Task<PagedResult<PromoCodeDto>> ListPromoCodes([FromQuery] PromoCodeQuery query, CancellationToken ct) => promoCodes.ListAsync(query, ct);

    [HttpPost("finance/promo-codes")]
    [ProducesResponseType<PromoCodeDto>(StatusCodes.Status201Created)]
    public async Task<ObjectResult> CreatePromoCode(CreatePromoCodeRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await promoCodes.CreateAsync(request, ct));

    [HttpPatch("finance/promo-codes/{id:guid}")]
    public Task<PromoCodeDto> UpdatePromoCode(Guid id, UpdatePromoCodeRequest request, CancellationToken ct) => promoCodes.UpdateAsync(id, request, ct);

    [HttpPost("finance/promo-codes/{id:guid}/deactivate")]
    public Task<PromoCodeDto> DeactivatePromoCode(Guid id, CancellationToken ct) => promoCodes.DeactivateAsync(id, ct);
}
