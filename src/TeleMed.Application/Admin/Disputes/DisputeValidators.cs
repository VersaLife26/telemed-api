using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Admin.Disputes;

public sealed class DisputeQueryValidator : AbstractValidator<DisputeQuery>
{
    public DisputeQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class CreateDisputeRequestValidator : AbstractValidator<CreateDisputeRequest>
{
    public CreateDisputeRequestValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(4000);
    }
}

public sealed class AddDisputeCommentRequestValidator : AbstractValidator<AddDisputeCommentRequest>
{
    public AddDisputeCommentRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class ResolveDisputeRequestValidator : AbstractValidator<ResolveDisputeRequest>
{
    public ResolveDisputeRequestValidator()
    {
        RuleFor(x => x.Resolution).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.RefundAmountCents).GreaterThan(0).When(x => x.RefundAmountCents is not null);
    }
}
