using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Admin.Doctors;

public sealed class AdminDoctorQueryValidator : AbstractValidator<AdminDoctorQuery>
{
    public AdminDoctorQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Q).MaximumLength(100);
    }
}

public sealed class SuspendDoctorRequestValidator : AbstractValidator<SuspendDoctorRequest>
{
    public SuspendDoctorRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
