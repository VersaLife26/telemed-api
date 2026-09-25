using TeleMed.Application.Abstractions;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Scheduling;

internal static class DoctorLookup
{
    public static async Task<Doctor> MineAsync(this IDoctorRepository doctors, ICurrentActor actor, CancellationToken ct) =>
        await doctors.FindByUserIdAsync(actor.RequireUserId(), ct) ?? throw new NotFoundException("Doctor profile not found.");

    public static async Task<Doctor> MineWritableAsync(this IDoctorRepository doctors, ICurrentActor actor, CancellationToken ct)
    {
        var doctor = await doctors.MineAsync(actor, ct);
        return doctor.Status == DoctorStatus.Suspended
            ? throw new ForbiddenException("Your doctor profile is suspended.", "doctor_suspended")
            : doctor;
    }

    public static async Task<Doctor> RequireAsync(this IDoctorRepository doctors, Guid id, CancellationToken ct) =>
        await doctors.FindAsync(id, ct) ?? throw new NotFoundException("Doctor not found.");
}
