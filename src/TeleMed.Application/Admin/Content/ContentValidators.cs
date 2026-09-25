using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Admin.Content;

public sealed class CreateSpecialtyRequestValidator : AbstractValidator<CreateSpecialtyRequest>
{
    public CreateSpecialtyRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches("^[a-z][a-z0-9_]{1,49}$")
            .WithMessage("Code must be 2-50 lowercase letters, digits or underscores, starting with a letter.");
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameSi).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameTa).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayOrder).InclusiveBetween(0, 10_000);
    }
}

public sealed class UpdateSpecialtyRequestValidator : AbstractValidator<UpdateSpecialtyRequest>
{
    public UpdateSpecialtyRequestValidator()
    {
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameSi).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameTa).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayOrder).InclusiveBetween(0, 10_000);
    }
}

public sealed class SaveDrugRequestValidator : AbstractValidator<SaveDrugRequest>
{
    public SaveDrugRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.GenericName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Strength).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Form).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Manufacturer).MaximumLength(200);
        RuleFor(x => x.Category).MaximumLength(200);
    }
}

public sealed class AdminDrugQueryValidator : AbstractValidator<AdminDrugQuery>
{
    public AdminDrugQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Q).MaximumLength(100);
    }
}
