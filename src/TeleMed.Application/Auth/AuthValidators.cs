using FluentValidation;

namespace TeleMed.Application.Auth;

internal static class AuthRules
{
    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(8).MaximumLength(128);

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(254).EmailAddress();

    public static void DestinationRules<T>(this AbstractValidator<T> validator, Func<T, string?> phone, Func<T, string?> email)
    {
        validator.RuleFor(x => x)
            .Must(x => OtpDestination.Parse(phone(x), email(x)) is not null)
            .OverridePropertyName("destination")
            .WithMessage("Provide either a valid Sri Lankan mobile number or an email address.");
        validator.RuleFor(x => email(x)!).ValidEmail().OverridePropertyName("email").When(x => !string.IsNullOrWhiteSpace(email(x)));
    }
}

public sealed class OtpSendRequestValidator : AbstractValidator<OtpSendRequest>
{
    public OtpSendRequestValidator()
    {
        this.DestinationRules(x => x.Phone, x => x.Email);
        RuleFor(x => x.Language).IsInEnum();
    }
}

public sealed class OtpVerifyRequestValidator : AbstractValidator<OtpVerifyRequest>
{
    public OtpVerifyRequestValidator()
    {
        this.DestinationRules(x => x.Phone, x => x.Email);
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{6}$");
        RuleFor(x => x.Language).IsInEnum();
    }
}

public sealed class RegisterEmailRequestValidator : AbstractValidator<RegisterEmailRequest>
{
    public RegisterEmailRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).ValidPassword();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Language).IsInEnum();
    }
}

public sealed class LoginEmailRequestValidator : AbstractValidator<LoginEmailRequest>
{
    public LoginEmailRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequest>
{
    public GoogleLoginRequestValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty().MaximumLength(8192);
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(200);
    }
}
