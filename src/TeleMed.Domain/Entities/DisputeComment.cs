using TeleMed.Domain.Common;

namespace TeleMed.Domain.Entities;

public class DisputeComment : Entity, IAuditable
{
    public Guid DisputeId { get; init; }
    public Guid AuthorAdminId { get; init; }
    public required string Body { get; init; }
}
