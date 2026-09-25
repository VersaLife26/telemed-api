using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

public class WorkingHour : Entity, IAuditable
{
    public Guid DoctorId { get; init; }
    public DayOfWeek DayOfWeek { get; init; }
    public int StartMinute { get; init; }
    public int EndMinute { get; init; }
}
