using FluentValidation;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Doctors;

internal static class DoctorRules
{
    public const int MaxExperienceYears = 70;

    public static IRuleBuilderOptions<T, long> ValidFee<T>(this IRuleBuilder<T, long> rule) =>
        rule.InclusiveBetween(PlatformPolicy.MinFeeCents, PlatformPolicy.MaxFeeCents)
            .WithMessage($"Consultation fee must be between LKR {PlatformPolicy.MinFeeCents / 100} and LKR {PlatformPolicy.MaxFeeCents / 100}.");

    public static void LanguageRules<T>(this AbstractValidator<T> validator, Func<T, IReadOnlyList<ConsultationLanguage>?> languages, Func<T, string?> other)
    {
        validator.RuleFor(x => languages(x)).NotEmpty().OverridePropertyName("languages");
        validator.RuleForEach(x => languages(x)).IsInEnum().OverridePropertyName("languages");
        validator.RuleFor(x => languages(x))
            .Must(l => l is null || l.Distinct().Count() == l.Count)
            .OverridePropertyName("languages")
            .WithMessage("Languages must not repeat.");
        validator.RuleFor(x => other(x)).NotEmpty().MaximumLength(100)
            .OverridePropertyName("languageOther")
            .When(x => languages(x)?.Contains(ConsultationLanguage.Other) == true)
            .WithMessage("Specify the other language.");
        validator.RuleFor(x => other(x)).Empty()
            .OverridePropertyName("languageOther")
            .When(x => languages(x)?.Contains(ConsultationLanguage.Other) != true)
            .WithMessage("Only set languageOther when 'other' is one of the languages.");
    }
}

public sealed class QualificationDtoValidator : AbstractValidator<QualificationDto>
{
    public QualificationDtoValidator(TimeProvider time)
    {
        RuleFor(x => x.Degree).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Institution).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Year).InclusiveBetween(1950, time.GetUtcNow().Year).When(x => x.Year is not null);
    }
}

public sealed class UpdateDoctorProfileRequestValidator : AbstractValidator<UpdateDoctorProfileRequest>
{
    public UpdateDoctorProfileRequestValidator(TimeProvider time)
    {
        RuleFor(x => x.DisplayName).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Bio).MaximumLength(2000);
        RuleFor(x => x.SubSpecialties).Must(s => s is null || s.Count <= 10).WithMessage("At most 10 sub-specialties.");
        RuleForEach(x => x.SubSpecialties).NotEmpty().MaximumLength(100);
        this.LanguageRules(x => x.Languages, x => x.LanguageOther);
        RuleFor(x => x.Qualifications).Must(q => q is null || q.Count <= 20).WithMessage("At most 20 qualifications.");
        RuleForEach(x => x.Qualifications).SetValidator(new QualificationDtoValidator(time));
        RuleFor(x => x.ExperienceYears).InclusiveBetween(0, DoctorRules.MaxExperienceYears);
        RuleFor(x => x.FeeCents).ValidFee();
    }
}

public sealed class DoctorSearchQueryValidator : AbstractValidator<DoctorSearchQuery>
{
    public DoctorSearchQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.Specialty).MaximumLength(50);
        RuleFor(x => x.Language).IsInEnum();
        RuleFor(x => x.Q).MaximumLength(100);
        RuleFor(x => x.MinFee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxFee).GreaterThanOrEqualTo(x => x.MinFee ?? 0);
        RuleFor(x => x.Sort).IsInEnum();
    }
}
