using FluentValidation;

namespace TeleMed.Application.Admin.AdminUsers;

public sealed class CreateAdminUserRequestValidator : AbstractValidator<CreateAdminUserRequest>
{
    public CreateAdminUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public sealed class UpdateAdminUserRequestValidator : AbstractValidator<UpdateAdminUserRequest>
{
    public UpdateAdminUserRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200).When(x => x.DisplayName is not null);
        RuleFor(x => x.Role).IsInEnum();
    }
}
