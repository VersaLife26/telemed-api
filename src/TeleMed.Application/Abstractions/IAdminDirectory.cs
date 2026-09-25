namespace TeleMed.Application.Abstractions;

public interface IAdminDirectory
{
    Task<AdminActor?> FindActiveAsync(string email, CancellationToken ct);
    void Evict(string email);
}
