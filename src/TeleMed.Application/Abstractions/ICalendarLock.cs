namespace TeleMed.Application.Abstractions;

// Transaction-scoped; call inside BeginTransactionAsync. Lock order: platform, then doctors in id order.
public interface ICalendarLock
{
    // Serialises writes to one doctor's calendar, and holds off platform-wide holidays while it does.
    Task LockDoctorAsync(Guid doctorId, CancellationToken ct);

    // Excludes every doctor calendar write; taken when a platform-wide holiday is added.
    Task LockPlatformAsync(CancellationToken ct);
}
