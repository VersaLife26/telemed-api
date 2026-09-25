namespace TeleMed.Domain.Entities;

public sealed record VerificationChecklist(
    ChecklistItem? SlmcFormat = null,
    ChecklistItem? SlmcRegistry = null,
    ChecklistItem? Experience = null,
    ChecklistItem? NicMatch = null,
    ChecklistItem? PhotoClarity = null);

public sealed record ChecklistItem(bool Ok, Guid ByAdminId, DateTimeOffset At);
