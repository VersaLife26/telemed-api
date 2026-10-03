using FluentValidation;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Admin.Finance;

public sealed class UpdateCommissionRequestValidator : AbstractValidator<UpdateCommissionRequest>
{
    public UpdateCommissionRequestValidator()
    {
        RuleFor(x => x.CommissionBps).InclusiveBetween(0, PlatformPolicy.MaxCommissionBps);
    }
}

public sealed class SetDoctorCommissionRequestValidator : AbstractValidator<SetDoctorCommissionRequest>
{
    public SetDoctorCommissionRequestValidator()
    {
        RuleFor(x => x.CommissionBps).InclusiveBetween(0, PlatformPolicy.MaxCommissionBps)
            .When(x => x.CommissionBps is not null);
    }
}

public sealed class LedgerQueryValidator : AbstractValidator<LedgerQuery>
{
    public LedgerQueryValidator()
    {
        Include(new PageQueryValidator());
        Include(new DateRangeValidator());
    }
}

public sealed class LedgerExportQueryValidator : AbstractValidator<LedgerExportQuery>
{
    public LedgerExportQueryValidator()
    {
        Include(new DateRangeValidator());
    }
}

public sealed class AdminRefundQueryValidator : AbstractValidator<AdminRefundQuery>
{
    public AdminRefundQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class CreateRefundRequestValidator : AbstractValidator<CreateRefundRequest>
{
    public CreateRefundRequestValidator()
    {
        RuleFor(x => x.AmountCents).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class RejectRefundRequestValidator : AbstractValidator<RejectRefundRequest>
{
    public RejectRefundRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class MarkRefundedRequestValidator : AbstractValidator<MarkRefundedRequest>
{
    public MarkRefundedRequestValidator()
    {
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(100);
    }
}

public sealed class PromoCodeQueryValidator : AbstractValidator<PromoCodeQuery>
{
    public PromoCodeQueryValidator()
    {
        Include(new PageQueryValidator());
    }
}

public sealed class CreatePromoCodeRequestValidator : AbstractValidator<CreatePromoCodeRequest>
{
    public CreatePromoCodeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches("^[A-Za-z0-9_-]{3,50}$")
            .WithMessage("Use 3 to 50 letters, digits, dashes or underscores.");
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.DiscountType).IsInEnum();
        When(x => x.DiscountType == PromoDiscountType.Percent, () =>
        {
            RuleFor(x => x.PercentBps).NotNull().InclusiveBetween(1, 10_000);
            RuleFor(x => x.AmountOffCents).Null();
            RuleFor(x => x.MaxDiscountCents).GreaterThan(0).When(x => x.MaxDiscountCents is not null);
        });
        When(x => x.DiscountType == PromoDiscountType.Fixed, () =>
        {
            RuleFor(x => x.AmountOffCents).NotNull().GreaterThan(0);
            RuleFor(x => x.PercentBps).Null();
            RuleFor(x => x.MaxDiscountCents).Null();
        });
        RuleFor(x => x.MinAmountCents).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidUntil).GreaterThan(x => x.ValidFrom).When(x => x.ValidFrom is not null && x.ValidUntil is not null);
        RuleFor(x => x.MaxRedemptions).GreaterThan(0).When(x => x.MaxRedemptions is not null);
        RuleFor(x => x.MaxPerUser).GreaterThan(0);
    }
}

public sealed class UpdatePromoCodeRequestValidator : AbstractValidator<UpdatePromoCodeRequest>
{
    public UpdatePromoCodeRequestValidator()
    {
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.MaxRedemptions).GreaterThan(0).When(x => x.MaxRedemptions is not null);
        RuleFor(x => x.MaxPerUser).GreaterThan(0).When(x => x.MaxPerUser is not null);
    }
}
