using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Testing;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class CapturedMessageMapper
{
    public static partial CapturedMessageDto ToDto(this CapturedMessage message);
}
