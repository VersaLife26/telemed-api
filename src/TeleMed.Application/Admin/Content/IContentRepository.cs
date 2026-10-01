using TeleMed.Domain.Entities;

namespace TeleMed.Application.Admin.Content;

public interface IContentRepository
{
    Task<IReadOnlyList<Specialty>> ListSpecialtiesAsync(CancellationToken ct);
    Task<Specialty?> FindSpecialtyAsync(string code, CancellationToken ct);
    void AddSpecialty(Specialty specialty);
    void RemoveSpecialty(Specialty specialty);

    Task<(IReadOnlyList<Drug> Items, long Total)> ListDrugsAsync(string? search, int skip, int take, CancellationToken ct);
    Task<Drug?> FindDrugAsync(Guid id, CancellationToken ct);
    void AddDrug(Drug drug);
    void RemoveDrug(Drug drug);

    Task<IReadOnlyList<WaitingRoomItem>> ListWaitingRoomItemsAsync(bool activeOnly, CancellationToken ct);
    Task<WaitingRoomItem?> FindWaitingRoomItemAsync(Guid id, CancellationToken ct);
    void AddWaitingRoomItem(WaitingRoomItem item);
    void RemoveWaitingRoomItem(WaitingRoomItem item);
}
