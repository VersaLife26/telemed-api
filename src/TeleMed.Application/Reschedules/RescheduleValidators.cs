using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Reschedules;

public sealed class ProposeRescheduleRequestValidator : AbstractValidator<ProposeRescheduleRequest>
{
    public ProposeRescheduleRequestValidator()
    {
        RuleFor(x => x.ProposedStartAt).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class RescheduleQueryValidator : AbstractValidator<RescheduleQuery>
{
    public RescheduleQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Status).IsInEnum();
    }
}
