using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Payments;

public sealed class ApplyPromoRequestValidator : AbstractValidator<ApplyPromoRequest>
{
    public ApplyPromoRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    }
}

public sealed class CreateIntentRequestValidator : AbstractValidator<CreateIntentRequest>
{
    public CreateIntentRequestValidator()
    {
        RuleFor(x => x.Provider).IsInEnum();
    }
}

public sealed class MockCompleteRequestValidator : AbstractValidator<MockCompleteRequest>
{
    public MockCompleteRequestValidator()
    {
        RuleFor(x => x.Outcome).IsInEnum();
    }
}

public sealed class PaymentQueryValidator : AbstractValidator<PaymentQuery>
{
    public PaymentQueryValidator()
    {
        Include(new PageQueryValidator());
    }
}
