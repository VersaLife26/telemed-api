using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Admin.Users;

public sealed class PlatformUserQueryValidator : AbstractValidator<PlatformUserQuery>
{
    public PlatformUserQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Q).MaximumLength(100);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class SuspendUserRequestValidator : AbstractValidator<SuspendUserRequest>
{
    public SuspendUserRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
