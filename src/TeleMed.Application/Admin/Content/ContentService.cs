using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.Content;

public sealed class ContentService(IContentRepository repository, IUnitOfWork unitOfWork)
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

    private async Task<Specialty> LoadSpecialtyAsync(string code, CancellationToken ct) =>
        await repository.FindSpecialtyAsync(code, ct) ?? throw new NotFoundException("Specialty not found.");

    private async Task<Drug> LoadDrugAsync(Guid id, CancellationToken ct) =>
        await repository.FindDrugAsync(id, ct) ?? throw new NotFoundException("Drug not found.");
}
