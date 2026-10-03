using FluentValidation;
using TeleMed.Application.Common;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Admin.Doctors;

public sealed class AdminDoctorQueryValidator : AbstractValidator<AdminDoctorQuery>
{
    public AdminDoctorQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Q).MaximumLength(100);
    }
}

public sealed class SuspendDoctorRequestValidator : AbstractValidator<SuspendDoctorRequest>
{
    public SuspendDoctorRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class SetForeignMultiplierRequestValidator : AbstractValidator<SetForeignMultiplierRequest>
{
    public SetForeignMultiplierRequestValidator()
    {
        RuleFor(x => x.Multiplier)
            .InclusiveBetween(ForeignPricing.MinMultiplier, ForeignPricing.MaxMultiplier)
            .Must(value => value == decimal.Round(value, 2))
            .WithMessage("Use at most 2 decimal places.");
    }
}
