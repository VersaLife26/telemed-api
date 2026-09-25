using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Vault;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class VaultMapper
{
    public static partial VaultDocumentDto ToDto(this VaultDocument document);

    public static partial VaultFolderDto ToDto(this VaultFolder folder);
}
