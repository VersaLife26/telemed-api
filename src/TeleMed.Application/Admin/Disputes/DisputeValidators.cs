using FluentValidation;
using TeleMed.Application.Admin.Disputes;
using TeleMed.Application.Common;
using TeleMed.Application.CustomerCare;

namespace TeleMed.Application.Admin.Disputes;

public sealed class DisputeQueryValidator : AbstractValidator<DisputeQuery>
{
    public DisputeQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Category).IsInEnum();
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

public sealed class OpenCustomerCareRequestValidator : AbstractValidator<OpenCustomerCareRequest>
{
    public OpenCustomerCareRequestValidator()
    {
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class CustomerCareMessageRequestValidator : AbstractValidator<CustomerCareMessageRequest>
{
    public CustomerCareMessageRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

public sealed class CustomerCareQueryValidator : AbstractValidator<CustomerCareQuery>
{
    public CustomerCareQueryValidator() => Include(new PageQueryValidator());
}
