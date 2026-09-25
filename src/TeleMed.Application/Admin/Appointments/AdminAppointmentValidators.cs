using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Admin.Appointments;

public sealed class AdminAppointmentQueryValidator : AbstractValidator<AdminAppointmentQuery>
{
    public AdminAppointmentQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.To).GreaterThan(x => x.From).When(x => x.From is not null && x.To is not null);
    }
}

public sealed class AdminCancelAppointmentRequestValidator : AbstractValidator<AdminCancelAppointmentRequest>
{
    public AdminCancelAppointmentRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
