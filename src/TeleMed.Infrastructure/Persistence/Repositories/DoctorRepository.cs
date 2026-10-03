using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using TeleMed.Application.Admin.Doctors;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Persistence.Configurations;

namespace TeleMed.Infrastructure.Persistence.Repositories;

internal sealed class DoctorRepository(AppDbContext db) : IDoctorRepository
{
    private const string TextSearchConfig = "english";

    public void Add(Doctor doctor) => db.Doctors.Add(doctor);

    public Task<Doctor?> FindAsync(Guid id, CancellationToken ct) => db.Doctors.SingleOrDefaultAsync(d => d.Id == id, ct);

    public Task<Doctor?> FindByUserIdAsync(Guid userId, CancellationToken ct) => db.Doctors.SingleOrDefaultAsync(d => d.UserId == userId, ct);

    public Task<Doctor?> FindByUserPhoneAsync(string phone, CancellationToken ct) =>
        db.Doctors.AsNoTracking()
            .Where(d => db.Users.Any(u => u.Id == d.UserId && u.PhoneNumber == phone && u.Status != UserStatus.Deleted))
            .FirstOrDefaultAsync(ct);

    public Task<bool> SlmcNumberExistsAsync(string slmcNumber, CancellationToken ct) => db.Doctors.AnyAsync(d => d.SlmcNumber == slmcNumber, ct);

    public Task<Doctor?> FindListedAsync(Guid id, CancellationToken ct) => Listed.SingleOrDefaultAsync(d => d.Id == id, ct);

    public async Task<(IReadOnlyList<Doctor> Items, long Total)> SearchListedAsync(DoctorSearchFilter filter, CancellationToken ct)
    {
        var query = Listed;
        if (filter.Specialty is { } specialty)
        {
            query = query.Where(d => d.SpecialtyCode == specialty);
        }

        if (filter.Language is { } language)
        {
            query = query.Where(d => d.Languages.Contains(language));
        }

        if (filter.MinFee is { } minFee)
        {
            query = query.Where(d => d.FeeCents >= minFee);
        }

        if (filter.MaxFee is { } maxFee)
        {
            query = query.Where(d => d.FeeCents <= maxFee);
        }

        if (filter.Text is { } text)
        {
            query = query.Where(d => EF.Property<NpgsqlTsVector>(d, DoctorConfiguration.SearchVector)
                .Matches(EF.Functions.WebSearchToTsQuery(TextSearchConfig, text)));
        }

        var total = await query.LongCountAsync(ct);
        var ordered = filter.Sort switch
        {
            DoctorSort.Fee => query.OrderBy(d => d.FeeCents).ThenBy(d => d.DisplayName),
            DoctorSort.Experience => query.OrderByDescending(d => d.ExperienceYears).ThenBy(d => d.DisplayName),
            null when filter.Text is { } rankText => query
                .OrderByDescending(d => EF.Property<NpgsqlTsVector>(d, DoctorConfiguration.SearchVector)
                    .Rank(EF.Functions.WebSearchToTsQuery(TextSearchConfig, rankText)))
                .ThenBy(d => d.DisplayName),
            _ => query.OrderBy(d => d.DisplayName),
        };
        var items = await ordered.ThenBy(d => d.Id).Skip(filter.Skip).Take(filter.Take).ToListAsync(ct);
        return (items, total);
    }

    public async Task<(IReadOnlyList<AdminDoctorListItemDto> Items, long Total)> ListForAdminAsync(
        DoctorStatus? status, string? search, int skip, int take, CancellationToken ct)
    {
        var query = from d in db.Doctors
                    join u in db.Users on d.UserId equals u.Id
                    select new { Doctor = d, User = u };
        if (status is { } s)
        {
            query = query.Where(x => x.Doctor.Status == s);
        }

        if (search is not null)
        {
            var lowered = search.ToLowerInvariant();
            query = query.Where(x => x.Doctor.DisplayName.ToLower().Contains(lowered)
                || x.Doctor.SlmcNumber.ToLower().Contains(lowered)
                || (x.User.PhoneNumber != null && x.User.PhoneNumber.Contains(search))
                || (x.User.Email != null && x.User.Email.ToLower().Contains(lowered)));
        }

        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.Doctor.CreatedAt).ThenBy(x => x.Doctor.Id)
            .Skip(skip).Take(take)
            .Select(x => new AdminDoctorListItemDto(
                x.Doctor.Id,
                x.Doctor.DisplayName,
                x.Doctor.SlmcNumber,
                x.Doctor.SpecialtyCode,
                x.Doctor.Status,
                x.User.PhoneNumber,
                x.User.Email,
                x.Doctor.FeeCents,
                x.Doctor.CommissionBps,
                x.Doctor.ForeignMultiplier,
                x.Doctor.CreatedAt))
            .ToListAsync(ct);
        return (items, total);
    }

    private IQueryable<Doctor> Listed =>
        db.Doctors.AsNoTracking()
            .Where(d => d.Status == DoctorStatus.Active && db.Users.Any(u => u.Id == d.UserId && u.Status == UserStatus.Active));
}
