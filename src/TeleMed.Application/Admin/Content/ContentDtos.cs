using TeleMed.Application.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Content;

public sealed record AdminSpecialtyDto(string Code, string NameEn, string NameSi, string NameTa, int DisplayOrder, bool IsActive);

public sealed record CreateSpecialtyRequest(string Code, string NameEn, string NameSi, string NameTa, int DisplayOrder, bool IsActive = true);

public sealed record UpdateSpecialtyRequest(string NameEn, string NameSi, string NameTa, int DisplayOrder, bool IsActive = true);

public sealed record AdminDrugDto(
    Guid Id,
    string Name,
    string GenericName,
    string Strength,
    string Form,
    string? Manufacturer,
    string? Category,
    bool IsControlled,
    bool IsGeneric,
    bool IsActive);

public sealed record SaveDrugRequest(
    string Name,
    string GenericName,
    string Strength,
    string Form,
    string? Manufacturer,
    string? Category,
    bool IsControlled,
    bool IsGeneric,
    bool IsActive = true);

public sealed record AdminDrugQuery : PageQuery
{
    public string? Q { get; init; }
}

public sealed record WaitingRoomItemDto(
    Guid Id,
    WaitingRoomItemKind Kind,
    string Title,
    string? Body,
    string? LinkUrl,
    string? VideoUrl,
    string? ImageUrl,
    string? VideoFileUrl,
    int DisplayOrder);

public sealed record AdminWaitingRoomItemDto(
    Guid Id,
    WaitingRoomItemKind Kind,
    string Title,
    string? Body,
    string? LinkUrl,
    string? VideoUrl,
    string? ImageUrl,
    string? VideoFileUrl,
    int DisplayOrder,
    bool IsActive);

public sealed record SaveWaitingRoomItemRequest(
    WaitingRoomItemKind Kind,
    string Title,
    string? Body,
    string? LinkUrl,
    string? VideoUrl,
    int DisplayOrder,
    bool IsActive = true);
