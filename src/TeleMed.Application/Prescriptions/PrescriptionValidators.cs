using FluentValidation;
using TeleMed.Application.Common;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Prescriptions;

public sealed class IssuePrescriptionRequestValidator : AbstractValidator<IssuePrescriptionRequest>
{
    public IssuePrescriptionRequestValidator()
    {
        RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= Prescription.MaxItems)
            .WithMessage($"A prescription can have at most {Prescription.MaxItems} items.");
        RuleFor(x => x.Investigations).Must(i => i is null || i.Count <= Prescription.MaxInvestigations)
            .WithMessage($"A prescription can have at most {Prescription.MaxInvestigations} investigations.");
        RuleForEach(x => x.Investigations).MaximumLength(300);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.DrugName).NotEmpty().Must(v => v.Trim().Length > 0).MaximumLength(200);
            item.RuleFor(x => x.Strength).MaximumLength(100);
            item.RuleFor(x => x.Form).MaximumLength(100);
            item.RuleFor(x => x.Dosage).NotEmpty().Must(v => v.Trim().Length > 0).MaximumLength(200);
            item.RuleFor(x => x.Frequency).NotEmpty().Must(v => v.Trim().Length > 0).MaximumLength(200);
            item.RuleFor(x => x.DurationDays).InclusiveBetween(1, 365);
            item.RuleFor(x => x.Quantity).InclusiveBetween(1, 10_000);
            item.RuleFor(x => x.Instructions).MaximumLength(1000);
        });
    }
}

public sealed class CancelPrescriptionRequestValidator : AbstractValidator<CancelPrescriptionRequest>
{
    public CancelPrescriptionRequestValidator() => RuleFor(x => x.Reason).MaximumLength(1000);
}

public sealed class PrescriptionQueryValidator : AbstractValidator<PrescriptionQuery>
{
    public PrescriptionQueryValidator() => Include(new PageQueryValidator());
}
