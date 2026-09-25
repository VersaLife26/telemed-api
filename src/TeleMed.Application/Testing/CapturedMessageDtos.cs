using TeleMed.Domain.Enums;

namespace TeleMed.Application.Testing;

public sealed record CapturedMessageDto(Guid Id, MessageChannel Channel, string Recipient, string? Subject, string Body, DateTimeOffset CreatedAt);

public sealed record CapturedMessageQuery(string? Recipient, MessageChannel? Channel);
