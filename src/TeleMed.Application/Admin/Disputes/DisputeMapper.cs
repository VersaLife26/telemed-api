using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.Disputes;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class DisputeMapper
{
    public static partial DisputeDto ToDto(this Dispute dispute);

    public static partial DisputeCommentDto ToDto(this DisputeComment comment);
}
