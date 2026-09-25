using TeleMed.Domain.Enums;

namespace TeleMed.Domain.Entities;

public class ClinicalNoteRevision
{
    public long Id { get; init; }
    public Guid NoteId { get; init; }
    public int Revision { get; init; }
    public required string Subjective { get; init; }
    public required string Objective { get; init; }
    public required string Assessment { get; init; }
    public required string Plan { get; init; }
    public IReadOnlyList<RevisionDiagnosis> Diagnoses { get; init; } = [];
    public ClinicalNoteChangeType ChangeType { get; init; }
    public string? AmendmentReason { get; init; }
    public Guid ChangedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record RevisionDiagnosis(string Code, string Display, bool IsPrimary);
