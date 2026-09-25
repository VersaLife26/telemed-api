using FluentValidation;
using TeleMed.Application.Auth;

namespace TeleMed.Application.Users;

public sealed class UpdateMeRequestValidator : AbstractValidator<UpdateMeRequest>
{
    public UpdateMeRequestValidator(TimeProvider time)
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Allergies).MaximumLength(2000);
        RuleFor(x => x.DateOfBirth)
            .InclusiveBetween(new DateOnly(1900, 1, 1), DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime))
            .When(x => x.DateOfBirth is not null);
        RuleFor(x => x.Sex).IsInEnum();
        RuleFor(x => x.Language).IsInEnum();
        RuleFor(x => x.Version).NotEmpty();
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).ValidPassword();
    }
}
