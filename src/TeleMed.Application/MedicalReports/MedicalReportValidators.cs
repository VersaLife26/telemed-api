using FluentValidation;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.MedicalReports;

public sealed class IssueMedicalReportRequestValidator : AbstractValidator<IssueMedicalReportRequest>
{
    public IssueMedicalReportRequestValidator()
    {
        RuleFor(x => x.Addressee).MaximumLength(200);
        RuleFor(x => x.ClinicalImpression).NotEmpty().Must(v => v.Trim().Length > 0).MaximumLength(500);
        RuleFor(x => x.Findings).MaximumLength(4000);
        RuleFor(x => x.Advice).MaximumLength(2000);
        RuleFor(x => x.FitnessNotes).MaximumLength(1000);
        RuleFor(x => x.Fitness).IsInEnum();

        When(x => x.Fitness is FitnessForWork.Unfit or FitnessForWork.Restricted, () =>
        {
            RuleFor(x => x.LeaveFrom).NotNull().WithMessage("Leave start date is required when fitness for work is limited.");
            RuleFor(x => x.LeaveUntil).NotNull().WithMessage("Leave end date is required when fitness for work is limited.");
        });

        When(x => x.LeaveFrom is not null && x.LeaveUntil is not null, () =>
        {
            RuleFor(x => x.LeaveUntil).GreaterThanOrEqualTo(x => x.LeaveFrom);
        });

        When(x => x.ReturnToWorkOn is not null && x.LeaveUntil is not null, () =>
        {
            RuleFor(x => x.ReturnToWorkOn).GreaterThanOrEqualTo(x => x.LeaveUntil);
        });
    }
}

public sealed class CancelMedicalReportRequestValidator : AbstractValidator<CancelMedicalReportRequest>
{
    public CancelMedicalReportRequestValidator() => RuleFor(x => x.Reason).MaximumLength(1000);
}

public sealed class MedicalReportQueryValidator : AbstractValidator<MedicalReportQuery>
{
    public MedicalReportQueryValidator() => Include(new PageQueryValidator());
}
