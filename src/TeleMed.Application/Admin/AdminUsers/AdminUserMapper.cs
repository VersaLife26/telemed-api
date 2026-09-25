using Riok.Mapperly.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.AdminUsers;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class AdminUserMapper
{
    public static partial AdminUserDto ToDto(this AdminUser admin);
}
