using Riok.Mapperly.Abstractions;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Users;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class UserMapper
{
    public static MeDto ToMeDto(this User user, IFileStorage storage) => user.Map() with
    {
        HasPassword = user.PasswordHash is not null,
        PhotoUrl = user.PhotoStorageKey is { } key ? storage.CreateSignedUrl(key, ProfilePhoto.ContentType(key)) : null,
    };

    [MapProperty(nameof(User.ConcurrencyStamp), nameof(MeDto.Version))]
    [MapperIgnoreTarget(nameof(MeDto.HasPassword))]
    [MapperIgnoreTarget(nameof(MeDto.PhotoUrl))]
    private static partial MeDto Map(this User user);
}
