using TeleMed.Domain.Entities;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Scheduling;

public interface ISchedulingRepository
{
    Task<IReadOnlyList<WorkingHour>> ListWorkingHoursAsync(Guid doctorId, CancellationToken ct);
    void AddWorkingHours(IEnumerable<WorkingHour> hours);
    void RemoveWorkingHours(IEnumerable<WorkingHour> hours);

    Task<Holiday?> FindHolidayAsync(Guid id, CancellationToken ct);
    // A doctor id returns that doctor's holidays plus platform-wide ones; null returns only platform-wide ones.
    Task<IReadOnlyList<Holiday>> ListHolidaysAsync(Guid? doctorId, DateOnly? from, DateOnly? to, CancellationToken ct);
    void AddHoliday(Holiday holiday);
    void RemoveHoliday(Holiday holiday);

    Task<SlotBlock?> FindSlotBlockAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<SlotBlock>> ListSlotBlocksAsync(Guid doctorId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct);
    void AddSlotBlock(SlotBlock block);
    void RemoveSlotBlock(SlotBlock block);

    // Everything that occupies the doctor's calendar in [from, to): slot blocks, live non-test appointments and pending reschedule
    // proposals (the hold). exceptAppointmentId leaves out one appointment and its own proposal.
    Task<IReadOnlyList<BusyInterval>> ListBusyAsync(Guid doctorId, DateTimeOffset from, DateTimeOffset to, Guid? exceptAppointmentId, CancellationToken ct);

    // Doctors with live non-test appointments or pending reschedule proposals overlapping [from, to), in ascending id order.
    Task<IReadOnlyList<Guid>> ListBookedDoctorIdsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
