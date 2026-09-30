using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Admin.Content;

public sealed class ContentService(IContentRepository repository, IUnitOfWork unitOfWork, IFileStorage storage)
{
    public async Task<IReadOnlyList<AdminSpecialtyDto>> ListSpecialtiesAsync(CancellationToken ct) =>
        (await repository.ListSpecialtiesAsync(ct)).Select(s => s.ToAdminDto()).ToList();

    public async Task<AdminSpecialtyDto> GetSpecialtyAsync(string code, CancellationToken ct) =>
        (await LoadSpecialtyAsync(code, ct)).ToAdminDto();

    public async Task<AdminSpecialtyDto> CreateSpecialtyAsync(CreateSpecialtyRequest request, CancellationToken ct)
    {
        var specialty = new Specialty
        {
            Code = request.Code,
            NameEn = request.NameEn.Trim(),
            NameSi = request.NameSi.Trim(),
            NameTa = request.NameTa.Trim(),
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
        };
        repository.AddSpecialty(specialty);
        await unitOfWork.SaveChangesAsync(ct);
        return specialty.ToAdminDto();
    }

    public async Task<AdminSpecialtyDto> UpdateSpecialtyAsync(string code, UpdateSpecialtyRequest request, CancellationToken ct)
    {
        var specialty = await LoadSpecialtyAsync(code, ct);
        specialty.NameEn = request.NameEn.Trim();
        specialty.NameSi = request.NameSi.Trim();
        specialty.NameTa = request.NameTa.Trim();
        specialty.DisplayOrder = request.DisplayOrder;
        specialty.IsActive = request.IsActive;
        await unitOfWork.SaveChangesAsync(ct);
        return specialty.ToAdminDto();
    }

    public async Task DeactivateSpecialtyAsync(string code, CancellationToken ct)
    {
        var specialty = await LoadSpecialtyAsync(code, ct);
        specialty.IsActive = false;
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AdminDrugDto>> ListDrugsAsync(AdminDrugQuery query, CancellationToken ct)
    {
        var search = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        var (items, total) = await repository.ListDrugsAsync(search, query.Skip, query.PageSize, ct);
        return new PagedResult<AdminDrugDto>(items.Select(d => d.ToAdminDto()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<AdminDrugDto> GetDrugAsync(Guid id, CancellationToken ct) => (await LoadDrugAsync(id, ct)).ToAdminDto();

    public async Task<AdminDrugDto> CreateDrugAsync(SaveDrugRequest request, CancellationToken ct)
    {
        var drug = new Drug
        {
            Name = request.Name.Trim(),
            GenericName = request.GenericName.Trim(),
            Strength = request.Strength.Trim(),
            Form = request.Form.Trim(),
        };
        Apply(drug, request);
        repository.AddDrug(drug);
        await unitOfWork.SaveChangesAsync(ct);
        return drug.ToAdminDto();
    }

    public async Task<AdminDrugDto> UpdateDrugAsync(Guid id, SaveDrugRequest request, CancellationToken ct)
    {
        var drug = await LoadDrugAsync(id, ct);
        Apply(drug, request);
        await unitOfWork.SaveChangesAsync(ct);
        return drug.ToAdminDto();
    }

    public async Task DeactivateDrugAsync(Guid id, CancellationToken ct)
    {
        var drug = await LoadDrugAsync(id, ct);
        drug.IsActive = false;
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AdminWaitingRoomItemDto>> ListWaitingRoomItemsAsync(CancellationToken ct) =>
        (await repository.ListWaitingRoomItemsAsync(activeOnly: false, ct)).Select(i => i.ToAdminDto(storage)).ToList();

    public async Task<IReadOnlyList<WaitingRoomItemDto>> ListPublishedWaitingRoomItemsAsync(CancellationToken ct) =>
        (await repository.ListWaitingRoomItemsAsync(activeOnly: true, ct)).Select(i => i.ToPublicDto(storage)).ToList();

    public async Task<AdminWaitingRoomItemDto> GetWaitingRoomItemAsync(Guid id, CancellationToken ct) =>
        (await LoadWaitingRoomItemAsync(id, ct)).ToAdminDto(storage);

    public async Task<AdminWaitingRoomItemDto> CreateWaitingRoomItemAsync(SaveWaitingRoomItemRequest request, CancellationToken ct)
    {
        var item = new WaitingRoomItem { Title = request.Title.Trim() };
        Apply(item, request);
        repository.AddWaitingRoomItem(item);
        await unitOfWork.SaveChangesAsync(ct);
        return item.ToAdminDto(storage);
    }

    public async Task<AdminWaitingRoomItemDto> UpdateWaitingRoomItemAsync(Guid id, SaveWaitingRoomItemRequest request, CancellationToken ct)
    {
        var item = await LoadWaitingRoomItemAsync(id, ct);
        Apply(item, request);
        await unitOfWork.SaveChangesAsync(ct);
        return item.ToAdminDto(storage);
    }

    public async Task DeactivateWaitingRoomItemAsync(Guid id, CancellationToken ct)
    {
        var item = await LoadWaitingRoomItemAsync(id, ct);
        item.IsActive = false;
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<AdminWaitingRoomItemDto> SetWaitingRoomItemImageAsync(Guid id, Stream content, CancellationToken ct)
    {
        var upload = await DoctorFiles.ReadAsync(content, PlatformPolicy.ProfilePhotoMaxBytes, FileSignature.Images, ct);
        var item = await LoadWaitingRoomItemAsync(id, ct);
        var key = $"waiting-room/{item.Id}/image-{Guid.CreateVersion7()}{FileSignature.Extension(upload.ContentType)}";
        using var stream = new MemoryStream(upload.Bytes, writable: false);
        await storage.SaveAsync(key, stream, ct);

        var previous = item.ImageStorageKey;
        item.ImageStorageKey = key;
        await unitOfWork.SaveChangesAsync(ct);
        if (previous is not null)
        {
            await storage.DeleteAsync(previous, ct);
        }

        return item.ToAdminDto(storage);
    }

    public async Task DeleteWaitingRoomItemImageAsync(Guid id, CancellationToken ct)
    {
        var item = await LoadWaitingRoomItemAsync(id, ct);
        var previous = item.ImageStorageKey ?? throw new NotFoundException("No image.");
        item.ImageStorageKey = null;
        await unitOfWork.SaveChangesAsync(ct);
        await storage.DeleteAsync(previous, ct);
    }

    private static void Apply(Drug drug, SaveDrugRequest request)
    {
        drug.Name = request.Name.Trim();
        drug.GenericName = request.GenericName.Trim();
        drug.Strength = request.Strength.Trim();
        drug.Form = request.Form.Trim();
        drug.Manufacturer = string.IsNullOrWhiteSpace(request.Manufacturer) ? null : request.Manufacturer.Trim();
        drug.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        drug.IsControlled = request.IsControlled;
        drug.IsGeneric = request.IsGeneric;
        drug.IsActive = request.IsActive;
    }

    private static void Apply(WaitingRoomItem item, SaveWaitingRoomItemRequest request)
    {
        item.Kind = request.Kind;
        item.Title = request.Title.Trim();
        item.Body = string.IsNullOrWhiteSpace(request.Body) ? null : request.Body.Trim();
        item.LinkUrl = string.IsNullOrWhiteSpace(request.LinkUrl) ? null : request.LinkUrl.Trim();
        item.VideoUrl = string.IsNullOrWhiteSpace(request.VideoUrl) ? null : request.VideoUrl.Trim();
        item.DisplayOrder = request.DisplayOrder;
        item.IsActive = request.IsActive;
    }

    private async Task<Specialty> LoadSpecialtyAsync(string code, CancellationToken ct) =>
        await repository.FindSpecialtyAsync(code, ct) ?? throw new NotFoundException("Specialty not found.");

    private async Task<Drug> LoadDrugAsync(Guid id, CancellationToken ct) =>
        await repository.FindDrugAsync(id, ct) ?? throw new NotFoundException("Drug not found.");

    private async Task<WaitingRoomItem> LoadWaitingRoomItemAsync(Guid id, CancellationToken ct) =>
        await repository.FindWaitingRoomItemAsync(id, ct) ?? throw new NotFoundException("Waiting room item not found.");
}
