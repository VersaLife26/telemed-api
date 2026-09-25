using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Admin.Credentialing;

public sealed class DoctorApplicationQueryValidator : AbstractValidator<DoctorApplicationQuery>
{
    public DoctorApplicationQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class UpdateChecklistRequestValidator : AbstractValidator<UpdateChecklistRequest>
{
    public UpdateChecklistRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.SlmcFormat is not null || x.SlmcRegistry is not null || x.Experience is not null || x.NicMatch is not null || x.PhotoClarity is not null)
            .OverridePropertyName("checklist")
            .WithMessage("Set at least one checklist item.");
    }
}

public sealed class RejectApplicationRequestValidator : AbstractValidator<RejectApplicationRequest>
{
    public RejectApplicationRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
