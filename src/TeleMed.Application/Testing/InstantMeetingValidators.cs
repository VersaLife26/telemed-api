using FluentValidation;

namespace TeleMed.Application.Testing;

public sealed class CreateInstantMeetingRequestValidator : AbstractValidator<CreateInstantMeetingRequest>
{
    public CreateInstantMeetingRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => new object?[] { x.CounterpartUserId, Blank(x.CounterpartPhone), Blank(x.CounterpartEmail) }.Count(v => v is not null) == 1)
            .WithName("counterpart")
            .WithMessage("Give exactly one of counterpartUserId, counterpartPhone or counterpartEmail.");
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
