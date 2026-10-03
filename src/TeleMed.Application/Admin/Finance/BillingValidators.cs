using FluentValidation;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Admin.Finance;

public sealed class UpdateBillingSettingsRequestValidator : AbstractValidator<UpdateBillingSettingsRequest>
{
    public UpdateBillingSettingsRequestValidator()
    {
        RuleFor(x => x.LkrPerUsd)
            .InclusiveBetween(ForeignPricing.MinLkrPerUsd, ForeignPricing.MaxLkrPerUsd)
            .Must(value => value == decimal.Round(value, 4))
            .WithMessage("Use at most 4 decimal places.");
    }
}
