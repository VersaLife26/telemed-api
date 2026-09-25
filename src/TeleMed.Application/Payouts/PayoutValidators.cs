using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Payouts;

public sealed class PayoutBatchQueryValidator : AbstractValidator<PayoutBatchQuery>
{
    public PayoutBatchQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class DoctorPayoutQueryValidator : AbstractValidator<DoctorPayoutQuery>
{
    public DoctorPayoutQueryValidator()
    {
        Include(new PageQueryValidator());
    }
}

public sealed class MarkPayoutPaidRequestValidator : AbstractValidator<MarkPayoutPaidRequest>
{
    public MarkPayoutPaidRequestValidator()
    {
        RuleFor(x => x.TransferReference).NotEmpty().MaximumLength(200);
    }
}

public sealed class MarkPayoutFailedRequestValidator : AbstractValidator<MarkPayoutFailedRequest>
{
    public MarkPayoutFailedRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
