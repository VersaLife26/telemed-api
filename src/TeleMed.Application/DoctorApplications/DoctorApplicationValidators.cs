using System.Text.RegularExpressions;
using FluentValidation;
using TeleMed.Application.Auth;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.DoctorApplications;

internal static partial class SlmcNumbers
{
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();

    public static bool IsValid(string? value) => value is not null && Format().IsMatch(Normalize(value));

    [GeneratedRegex(@"^(SLMC)?[A-Z]{0,3}\d{4,8}$")]
    private static partial Regex Format();
}

public sealed class BankDetailsRequestValidator : AbstractValidator<BankDetailsRequest>
{
    public BankDetailsRequestValidator()
    {
        RuleFor(x => x.BankName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BranchName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(34);
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(200);
    }
}

public sealed class DoctorApplicationRequestValidator : AbstractValidator<DoctorApplicationRequest>
{
    public DoctorApplicationRequestValidator()
    {
        RuleFor(x => x.TermsAccepted).Equal(true).WithMessage("You must accept the terms.");
        RuleFor(x => x.Phone).Must(p => PhoneNumber.NormalizeSriLankanMobile(p) is not null)
            .WithMessage("Enter a valid Sri Lankan mobile number.");
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password!).Length(8, 72)
            .Must(p => p.All(c => c == ' ' || !char.IsWhiteSpace(c)))
            .WithMessage("The password must be 8-72 characters without tabs or line breaks.")
            .When(x => x.Password is not null);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DisplayName!).MinimumLength(2).MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.DisplayName));
        RuleFor(x => x.SlmcNumber).Must(SlmcNumbers.IsValid).WithMessage("Enter a valid SLMC registration number.");
        RuleFor(x => x.SpecialtyCode).NotEmpty().MaximumLength(50);
        this.LanguageRules(x => x.Languages, x => x.LanguageOther);
        RuleFor(x => x.ExperienceYears).InclusiveBetween(0, DoctorRules.MaxExperienceYears);
        RuleFor(x => x.FeeCents).ValidFee();
        RuleFor(x => x.Bio).MaximumLength(2000);
        RuleFor(x => x.MedicalSchool).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.QualificationsText).NotEmpty().MinimumLength(2).MaximumLength(2000);
        RuleFor(x => x.AvailabilityNotes).NotEmpty().MinimumLength(2).MaximumLength(2000);
        RuleFor(x => x.PracticingLocations).NotEmpty().Must(l => l is null || l.Count <= 10).WithMessage("List between 1 and 10 practicing locations.");
        RuleForEach(x => x.PracticingLocations).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Bank).NotNull().SetValidator(new BankDetailsRequestValidator());
    }
}

public sealed class EligibilityQueryValidator : AbstractValidator<EligibilityQuery>
{
    public EligibilityQueryValidator()
    {
        RuleFor(x => x.Phone).Must(p => PhoneNumber.NormalizeSriLankanMobile(p) is not null)
            .WithMessage("Enter a valid Sri Lankan mobile number.");
    }
}
