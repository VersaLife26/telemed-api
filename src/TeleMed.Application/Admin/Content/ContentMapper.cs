using Riok.Mapperly.Abstractions;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Admin.Content;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
internal static partial class ContentMapper
{
    public static partial AdminSpecialtyDto ToAdminDto(this Specialty specialty);
    public static partial AdminDrugDto ToAdminDto(this Drug drug);

    public static AdminWaitingRoomItemDto ToAdminDto(this WaitingRoomItem item, IFileStorage storage) =>
        new(
            item.Id,
            item.Kind,
            item.Title,
            item.Body,
            item.LinkUrl,
            item.VideoUrl,
            ImageUrl(item, storage),
            item.DisplayOrder,
            item.IsActive);

    public static WaitingRoomItemDto ToPublicDto(this WaitingRoomItem item, IFileStorage storage) =>
        new(
            item.Id,
            item.Kind,
            item.Title,
            item.Body,
            item.LinkUrl,
            item.VideoUrl,
            ImageUrl(item, storage),
            item.DisplayOrder);

    private static string? ImageUrl(WaitingRoomItem item, IFileStorage storage) =>
        item.ImageStorageKey is { } key
            ? storage.CreateSignedUrl(key, FileSignature.ContentTypeFromExtension(Path.GetExtension(key)) ?? "application/octet-stream")
            : null;
}
