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
            SignedUrl(item.ImageStorageKey, storage),
            SignedUrl(item.VideoStorageKey, storage),
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
            SignedUrl(item.ImageStorageKey, storage),
            SignedUrl(item.VideoStorageKey, storage),
            item.DisplayOrder);

    private static string? SignedUrl(string? key, IFileStorage storage) =>
        key is { } storageKey
            ? storage.CreateSignedUrl(storageKey, FileSignature.ContentTypeFromExtension(Path.GetExtension(storageKey)) ?? "application/octet-stream")
            : null;
}
