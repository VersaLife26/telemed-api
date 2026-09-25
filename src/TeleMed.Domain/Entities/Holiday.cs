using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

public class Holiday : Entity, IAuditable
{
    public Guid? DoctorId { get; init; }
    public DateOnly Date { get; init; }
    public required string Reason { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public Guid? CreatedByAdminId { get; init; }
}
