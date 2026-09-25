using TeleMed.Domain.Common;
using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class PaymentWebhookEvent : Entity
{
    public PaymentProvider Provider { get; init; }
    public required string EventId { get; init; }
    public Guid? PaymentId { get; init; }
    public required string Outcome { get; init; }
    public required string Payload { get; init; }
}
