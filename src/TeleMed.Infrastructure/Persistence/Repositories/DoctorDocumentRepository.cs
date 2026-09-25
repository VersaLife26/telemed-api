using Microsoft.EntityFrameworkCore;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class DoctorDocumentRepository(AppDbContext db) : IDoctorDocumentRepository
{
    public void Add(DoctorDocument document) => db.DoctorDocuments.Add(document);

    public Task<DoctorDocument?> FindLiveAsync(Guid id, CancellationToken ct) =>
        Live.SingleOrDefaultAsync(d => d.Id == id, ct);

    public Task<DoctorDocument?> FindLiveForDoctorAsync(Guid doctorId, DoctorDocumentType type, CancellationToken ct) =>
        Live.Where(d => d.DoctorId == doctorId && d.Type == type).OrderByDescending(d => d.CreatedAt).FirstOrDefaultAsync(ct);

    public Task<DoctorDocument?> FindLiveForApplicationAsync(Guid applicationId, DoctorDocumentType type, CancellationToken ct) =>
        Live.SingleOrDefaultAsync(d => d.ApplicationId == applicationId && d.Type == type, ct);

    public async Task<IReadOnlyList<DoctorDocument>> ListLiveForDoctorAsync(Guid doctorId, CancellationToken ct) =>
        await Live.Where(d => d.DoctorId == doctorId).OrderBy(d => d.CreatedAt).ThenBy(d => d.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<DoctorDocument>> ListLiveForApplicationAsync(Guid applicationId, CancellationToken ct) =>
        await Live.Where(d => d.ApplicationId == applicationId).OrderBy(d => d.CreatedAt).ThenBy(d => d.Id).ToListAsync(ct);

    private IQueryable<DoctorDocument> Live => db.DoctorDocuments.Where(d => d.DeletedAt == null);
}
