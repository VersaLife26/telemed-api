using FluentValidation;
using TeleMed.Application.Common;

namespace TeleMed.Application.Appointments;

public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentRequestValidator()
    {
        RuleFor(x => x.DoctorId).NotEmpty();
        RuleFor(x => x.StartAt).NotEmpty();
        RuleFor(x => x.VisitPatient).NotNull().ChildRules(v =>
        {
            v.RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            v.RuleFor(x => x.DateOfBirth).NotEmpty().GreaterThan(new DateOnly(1900, 1, 1));
            v.RuleFor(x => x.Sex).IsInEnum();
            v.RuleFor(x => x.WeightKg).GreaterThan(0).LessThanOrEqualTo(500).PrecisionScale(4, 1, ignoreTrailingZeros: true);
            v.RuleFor(x => x.Allergies).MaximumLength(1000);
        });
        RuleFor(x => x.Intake).NotNull().ChildRules(i =>
        {
            i.RuleFor(x => x.Symptoms).NotEmpty().MaximumLength(2000);
            i.RuleFor(x => x.VisitRelation).MaximumLength(50);
        });
    }
}

public sealed class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(1000);
    }
}

public sealed class AppointmentQueryValidator : AbstractValidator<AppointmentQuery>
{
    public AppointmentQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.To).GreaterThan(x => x.From).When(x => x.From is not null && x.To is not null);
    }
}
