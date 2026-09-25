using FluentValidation;
using TeleMed.Application.Common;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Vault;

public sealed class VaultDocumentQueryValidator : AbstractValidator<VaultDocumentQuery>
{
    public VaultDocumentQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(x => x.FolderId).Must(FolderRef.IsValid).WithMessage("'Folder Id' must be \"root\" or a folder id.");
        RuleFor(x => x.DocumentType).IsInEnum();
    }
}

public sealed class VaultUploadFormValidator : AbstractValidator<VaultUploadForm>
{
    public VaultUploadFormValidator() => RuleFor(x => x.DocumentType).IsInEnum();
}

public sealed class UpdateVaultDocumentRequestValidator : AbstractValidator<UpdateVaultDocumentRequest>
{
    public UpdateVaultDocumentRequestValidator()
    {
        RuleFor(x => x).Must(x => x.FileName is not null || x.FolderId is not null).WithMessage("Nothing to change.").WithName("request");
        RuleFor(x => x.FileName!).Must(VaultNames.IsValidFileName).When(x => x.FileName is not null)
            .WithMessage($"File names must be 1-{VaultDocument.MaxFileNameLength} characters without slashes.");
        RuleFor(x => x.FolderId).Must(FolderRef.IsValid).WithMessage("'Folder Id' must be \"root\" or a folder id.");
    }
}

public sealed class CreateVaultFolderRequestValidator : AbstractValidator<CreateVaultFolderRequest>
{
    public CreateVaultFolderRequestValidator() =>
        RuleFor(x => x.Name).Must(VaultNames.IsValidFolderName)
            .WithMessage($"Folder names must be 1-{VaultFolder.MaxNameLength} characters without slashes.");
}

public sealed class UpdateVaultFolderRequestValidator : AbstractValidator<UpdateVaultFolderRequest>
{
    public UpdateVaultFolderRequestValidator()
    {
        RuleFor(x => x).Must(x => x.Name is not null || x.ParentId is not null).WithMessage("Nothing to change.").WithName("request");
        RuleFor(x => x.Name!).Must(VaultNames.IsValidFolderName).When(x => x.Name is not null)
            .WithMessage($"Folder names must be 1-{VaultFolder.MaxNameLength} characters without slashes.");
        RuleFor(x => x.ParentId).Must(FolderRef.IsValid).WithMessage("'Parent Id' must be \"root\" or a folder id.");
    }
}

internal static class VaultNames
{
    public static bool IsValidFolderName(string? name) =>
        name?.Trim() is { Length: > 0 and <= VaultFolder.MaxNameLength } trimmed && IsPlain(trimmed) && trimmed is not ("." or "..");

    public static bool IsValidFileName(string? name) =>
        name?.Trim() is { Length: > 0 and <= VaultDocument.MaxFileNameLength } trimmed && IsPlain(trimmed);

    private static bool IsPlain(string name) => !name.Any(c => c is '/' or '\\' || char.IsControl(c));
}
