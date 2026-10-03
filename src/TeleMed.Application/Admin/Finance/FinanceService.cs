using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Payouts;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Admin.Finance;

// The ledger is a view over captured payments and settled refunds, bucketed by Asia/Colombo dates.
public sealed class FinanceService(IFinanceRepository finance, ICommissionPolicy commission, IDoctorRepository doctors, IUnitOfWork unitOfWork, TimeProvider time)
{
    private static readonly string[] LedgerHeader =
    [
        "type", "id", "payment_id", "appointment_id", "doctor_id", "occurred_at", "amount_cents", "commission_cents",
        "provider_fee_cents", "payout_cents", "currency", "provider", "reference",
    ];

    public async Task<CommissionDto> GetCommissionAsync(CancellationToken ct) =>
        new(
            await commission.GetDefaultBpsAsync(ct),
            PlatformPolicy.ProviderFeeBps,
            PlatformPolicy.ProviderFeeFixedCents,
            PlatformPolicy.Currency,
            (int)PlatformPolicy.PayoutHold.TotalHours,
            await commission.ListDoctorRatesAsync(ct));

    public async Task<CommissionDto> UpdateDefaultAsync(UpdateCommissionRequest request, CancellationToken ct)
    {
        var policy = await commission.GetForUpdateAsync(ct);
        policy.DefaultCommissionBps = request.CommissionBps;
        await unitOfWork.SaveChangesAsync(ct);
        return await GetCommissionAsync(ct);
    }

    public async Task<CommissionDto> SetDoctorRateAsync(Guid doctorId, SetDoctorCommissionRequest request, CancellationToken ct)
    {
        var doctor = await doctors.FindAsync(doctorId, ct) ?? throw new NotFoundException("Doctor not found.");
        doctor.CommissionBps = request.CommissionBps;
        await unitOfWork.SaveChangesAsync(ct);
        return await GetCommissionAsync(ct);
    }

    public async Task<LedgerPageDto> ListLedgerAsync(LedgerQuery query, CancellationToken ct)
    {
        var filter = Filter(query, query.DoctorId);
        var (items, total) = await finance.ListLedgerAsync(filter, query.Skip, query.PageSize, ct);
        return new LedgerPageDto(items, query.Page, query.PageSize, total, await finance.SumLedgerAsync(filter, ct));
    }

    public Task WriteLedgerCsvAsync(LedgerExportQuery query, Stream output, CancellationToken ct) =>
        Csv.WriteAsync(
            output,
            LedgerHeader,
            finance.StreamLedgerAsync(Filter(query, query.DoctorId), ct),
            e => [e.Type, e.Id, e.PaymentId, e.AppointmentId, e.DoctorId, e.OccurredAt, e.AmountCents, e.CommissionCents, e.ProviderFeeCents, e.PayoutCents, e.Currency, e.Provider, e.Reference],
            ct);

    private LedgerFilter Filter(IDateRange range, Guid? doctorId)
    {
        var resolved = LocalDateRange.Resolve(range.From, range.To, PayoutService.Zone, time.GetUtcNow());
        return new LedgerFilter(resolved.FromUtc, resolved.ToUtc, doctorId);
    }
}
