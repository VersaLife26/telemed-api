using FluentValidation;
using TeleMed.Application.Common;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.ClinicalNotes;

public sealed class SaveClinicalNoteRequestValidator : AbstractValidator<SaveClinicalNoteRequest>
{
    public SaveClinicalNoteRequestValidator()
    {
        RuleFor(x => x.Subjective).MaximumLength(ClinicalNote.MaxSectionLength);
        RuleFor(x => x.Objective).MaximumLength(ClinicalNote.MaxSectionLength);
        RuleFor(x => x.Assessment).MaximumLength(ClinicalNote.MaxSectionLength);
        RuleFor(x => x.Plan).MaximumLength(ClinicalNote.MaxSectionLength);
        RuleFor(x => x.Diagnoses!).SetValidator(new DiagnosesValidator()).When(x => x.Diagnoses is not null);
    }
}

public sealed class AmendClinicalNoteRequestValidator : AbstractValidator<AmendClinicalNoteRequest>
{
    public AmendClinicalNoteRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().Must(r => r.Trim().Length > 0).MaximumLength(ClinicalNote.MaxAmendmentReasonLength);
        RuleFor(x => x.Subjective).MaximumLength(ClinicalNote.MaxSectionLength);
        RuleFor(x => x.Objective).MaximumLength(ClinicalNote.MaxSectionLength);
        RuleFor(x => x.Assessment).MaximumLength(ClinicalNote.MaxSectionLength);
        RuleFor(x => x.Plan).MaximumLength(ClinicalNote.MaxSectionLength);
        RuleFor(x => x.Diagnoses!).SetValidator(new DiagnosesValidator()).When(x => x.Diagnoses is not null);
    }
}

public sealed class ClinicalNoteQueryValidator : AbstractValidator<ClinicalNoteQuery>
{
    public ClinicalNoteQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Status).IsInEnum();
    }
}

internal sealed class DiagnosesValidator : AbstractValidator<IReadOnlyList<DiagnosisInput>>
{
    public DiagnosesValidator()
    {
        RuleFor(x => x.Count).LessThanOrEqualTo(ClinicalNote.MaxDiagnoses).OverridePropertyName("diagnoses");
        RuleForEach(x => x).ChildRules(d => d.RuleFor(x => x.Code).NotEmpty().MaximumLength(10)).OverridePropertyName("diagnoses");
        RuleFor(x => x)
            .Must(list => list.Select(d => ClinicalNoteService.NormaliseCode(d.Code)).Distinct().Count() == list.Count)
            .WithMessage("Each diagnosis code may appear only once.")
            .OverridePropertyName("diagnoses");
        RuleFor(x => x).Must(list => list.Count(d => d.IsPrimary) <= 1).WithMessage("Only one diagnosis may be primary.").OverridePropertyName("diagnoses");
    }
}
