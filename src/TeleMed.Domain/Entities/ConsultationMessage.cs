using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class ConsultationMessage : Entity
{
    public const int MaxBodyLength = 4000;

    public Guid ConsultationId { get; init; }
    public Guid SenderUserId { get; init; }
    public UserRole SenderRole { get; init; }
    public required string Body { get; init; }
}
