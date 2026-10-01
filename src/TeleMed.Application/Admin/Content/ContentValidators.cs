using FluentValidation;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

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

public sealed class SaveWaitingRoomItemRequestValidator : AbstractValidator<SaveWaitingRoomItemRequest>
{
    public SaveWaitingRoomItemRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).MaximumLength(20_000);
        RuleFor(x => x.Body).NotEmpty().When(x => x.Kind == WaitingRoomItemKind.Article)
            .WithMessage("Articles need a body patients can read.");
        RuleFor(x => x.LinkUrl).MaximumLength(2_000).Must(BeHttpUrl).When(x => !string.IsNullOrWhiteSpace(x.LinkUrl))
            .WithMessage("Link must be an http or https URL.");
        RuleFor(x => x.VideoUrl).MaximumLength(2_000).Must(BeHttpUrl).When(x => !string.IsNullOrWhiteSpace(x.VideoUrl))
            .WithMessage("Video must be an http or https URL.");
        RuleFor(x => x.DisplayOrder).InclusiveBetween(0, 10_000);
    }

    private static bool BeHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = $"https://{trimmed}";
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        return uri.HostNameType switch
        {
            UriHostNameType.IPv4 or UriHostNameType.IPv6 => true,
            UriHostNameType.Dns => uri.Host.Contains('.', StringComparison.Ordinal),
            _ => false,
        };
    }
}
