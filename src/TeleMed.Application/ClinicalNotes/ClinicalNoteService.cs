using FluentValidation;
using FluentValidation.Results;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Consultations;
using TeleMed.Application.Doctors;
using TeleMed.Application.Reference;
using TeleMed.Application.Scheduling;
using TeleMed.Application.Vault;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.ClinicalNotes;

// A draft is private to its author. Finalising signs it as revision 1; after that the only way to change it is an amendment,
// which needs a reason and appends a revision. Every read, finalise and amend is written to the record access log.
public sealed class ClinicalNoteService(
    ICurrentActor actor,
    IAppointmentRepository appointments,
    IDoctorRepository doctors,
    IConsultationRepository consultations,
    IClinicalNoteRepository notes,
    IReferenceDataRepository reference,
    VaultAccessPolicy policy,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public static string NormaliseCode(string? code) => (code ?? "").Trim().ToUpperInvariant();

    public async Task<PagedResult<ClinicalNoteSummaryDto>> ListMineAsync(ClinicalNoteQuery query, CancellationToken ct)
    {
        var doctor = await doctors.MineAsync(actor, ct);
        var (items, total) = await notes.ListForDoctorAsync(doctor.Id, query.Status, query.Skip, query.PageSize, ct);
        return new PagedResult<ClinicalNoteSummaryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<ClinicalNoteDto> GetAsync(Guid appointmentId, CancellationToken ct)
    {
        var note = await notes.FindByAppointmentAsync(appointmentId, ct) ?? throw NotFound();
        await AuthorizeReadAsync(note, RecordAccessAction.View, ct);
        return note.ToDto();
    }

    public async Task<IReadOnlyList<ClinicalNoteRevisionDto>> ListRevisionsAsync(Guid appointmentId, CancellationToken ct)
    {
        var note = await notes.FindByAppointmentAsync(appointmentId, ct) ?? throw NotFound();
        await AuthorizeReadAsync(note, RecordAccessAction.List, ct);
        return (await notes.ListRevisionsAsync(note.Id, ct)).Select(r => r.ToDto()).ToList();
    }

    public async Task<ClinicalNoteDto> SaveDraftAsync(Guid appointmentId, SaveClinicalNoteRequest request, CancellationToken ct)
    {
        var (appointment, doctor) = await TreatingAppointmentAsync(appointmentId, ct);
        if (await consultations.FindByAppointmentAsync(appointment.Id, ct) is not { StartedAt: not null })
        {
            throw new ConflictException("consultation_not_started", "Notes can be written once the consultation has started.");
        }

        var diagnoses = await ResolveAsync(request.Diagnoses ?? [], ct);
        var note = await notes.FindForUpdateByAppointmentAsync(appointment.Id, ct);
        if (note is null)
        {
            note = new ClinicalNote { AppointmentId = appointment.Id, DoctorId = doctor.Id, PatientId = appointment.PatientId };
            notes.Add(note);
        }
        else
        {
            if (note.Status == ClinicalNoteStatus.Finalised)
            {
                throw new ConflictException("note_finalised", "This note has been finalised; change it with an amendment.");
            }

            EnsureVersion(note, request.Version);
        }

        note.Subjective = request.Subjective ?? "";
        note.Objective = request.Objective ?? "";
        note.Assessment = request.Assessment ?? "";
        note.Plan = request.Plan ?? "";
        ReplaceDiagnoses(note, diagnoses);
        await unitOfWork.SaveChangesAsync(ct);
        return note.ToDto();
    }

    public async Task<ClinicalNoteDto> FinaliseAsync(Guid appointmentId, FinaliseClinicalNoteRequest? request, CancellationToken ct)
    {
        await TreatingAppointmentAsync(appointmentId, ct);
        var note = await notes.FindForUpdateByAppointmentAsync(appointmentId, ct) ?? throw NotFound();
        if (note.Status == ClinicalNoteStatus.Finalised)
        {
            throw new ConflictException("note_finalised", "This note has already been finalised.");
        }

        EnsureVersion(note, request?.Version);
        if (!note.HasContent)
        {
            throw new BadRequestException("note_empty", "Write at least one section before finalising the note.");
        }

        note.Status = ClinicalNoteStatus.Finalised;
        note.FinalisedAt = time.GetUtcNow();
        note.Revision = 1;
        notes.AddRevision(Snapshot(note, ClinicalNoteChangeType.Finalise, null));
        policy.Record(Access(note, RecordAccessAction.Finalise), true, VaultAccessPolicy.AuthorReason);
        await unitOfWork.SaveChangesAsync(ct);
        return note.ToDto();
    }

    public async Task<ClinicalNoteDto> AmendAsync(Guid appointmentId, AmendClinicalNoteRequest request, CancellationToken ct)
    {
        await TreatingAppointmentAsync(appointmentId, ct);
        var note = await notes.FindForUpdateByAppointmentAsync(appointmentId, ct) ?? throw NotFound();
        if (note.Status != ClinicalNoteStatus.Finalised)
        {
            throw new ConflictException("note_not_finalised", "Only a finalised note can be amended; edit the draft instead.");
        }

        EnsureVersion(note, request.Version);
        var diagnoses = request.Diagnoses is null ? null : await ResolveAsync(request.Diagnoses, ct);
        var changed = Differs(note.Subjective, request.Subjective)
            | Differs(note.Objective, request.Objective)
            | Differs(note.Assessment, request.Assessment)
            | Differs(note.Plan, request.Plan)
            | (diagnoses is not null && !SameDiagnoses(note, diagnoses));
        if (!changed)
        {
            throw new BadRequestException("no_change", "Change at least one section or diagnosis; a reason alone does not make an amendment.");
        }

        note.Subjective = request.Subjective ?? note.Subjective;
        note.Objective = request.Objective ?? note.Objective;
        note.Assessment = request.Assessment ?? note.Assessment;
        note.Plan = request.Plan ?? note.Plan;
        if (diagnoses is not null)
        {
            ReplaceDiagnoses(note, diagnoses);
        }

        if (!note.HasContent)
        {
            throw new BadRequestException("note_empty", "A finalised note cannot be amended to be empty.");
        }

        note.Revision++;
        notes.AddRevision(Snapshot(note, ClinicalNoteChangeType.Amend, request.Reason.Trim()));
        policy.Record(Access(note, RecordAccessAction.Amend), true, VaultAccessPolicy.AuthorReason);
        await unitOfWork.SaveChangesAsync(ct);
        return note.ToDto();
    }

    // Drafts are invisible to everyone but their author; a draft probe is logged and answered like a missing note.
    private async Task AuthorizeReadAsync(ClinicalNote note, RecordAccessAction action, CancellationToken ct)
    {
        if (note.Status == ClinicalNoteStatus.Draft)
        {
            var author = actor.Role == UserRole.Doctor && await doctors.FindByUserIdAsync(actor.RequireUserId(), ct) is { } doctor && doctor.Id == note.DoctorId;
            if (!author)
            {
                policy.Record(Access(note, action), false, "denied:draft_not_author");
                await unitOfWork.SaveChangesAsync(ct);
                throw NotFound();
            }
        }

        await policy.AuthorizeAsync(Access(note, action), ct);
    }

    // Only the appointment's own doctor writes its note; anyone else gets a 404 so appointment ids cannot be probed.
    private async Task<(Appointment Appointment, Doctor Doctor)> TreatingAppointmentAsync(Guid appointmentId, CancellationToken ct)
    {
        var doctor = await doctors.MineWritableAsync(actor, ct);
        var appointment = await appointments.FindAsync(appointmentId, ct);
        return appointment is not null && appointment.DoctorId == doctor.Id
            ? (appointment, doctor)
            : throw new NotFoundException("Appointment not found.");
    }

    private async Task<IReadOnlyList<DiagnosisDto>> ResolveAsync(IReadOnlyList<DiagnosisInput> inputs, CancellationToken ct)
    {
        if (inputs.Count == 0)
        {
            return [];
        }

        var codes = inputs.Select(d => NormaliseCode(d.Code)).ToList();
        var known = (await reference.FindIcd10Async(codes, ct)).ToDictionary(c => c.Code);
        var unknown = codes.Select((code, i) => (code, i)).Where(x => !known.ContainsKey(x.code)).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationException(unknown.Select(x =>
                new ValidationFailure($"diagnoses[{x.i}].code", $"{x.code} is not a known ICD-10 code.")));
        }

        return inputs.Select((d, i) => new DiagnosisDto(codes[i], known[codes[i]].Display, d.IsPrimary)).ToList();
    }

    private static bool SameDiagnoses(ClinicalNote note, IReadOnlyList<DiagnosisDto> diagnoses) =>
        note.Diagnoses.OrderBy(d => d.SortOrder).Select(d => (d.Code, d.IsPrimary)).SequenceEqual(diagnoses.Select(d => (d.Code, d.IsPrimary)));

    private void ReplaceDiagnoses(ClinicalNote note, IReadOnlyList<DiagnosisDto> diagnoses)
    {
        if (SameDiagnoses(note, diagnoses))
        {
            return;
        }

        note.Diagnoses.Clear();
        note.Diagnoses.AddRange(diagnoses.Select((d, i) => new ClinicalNoteDiagnosis
        {
            NoteId = note.Id,
            Code = d.Code,
            Display = d.Display,
            IsPrimary = d.IsPrimary,
            SortOrder = i,
        }));
        // Diagnoses live in their own table; touching the note keeps its version moving with them.
        note.UpdatedAt = time.GetUtcNow();
    }

    private ClinicalNoteRevision Snapshot(ClinicalNote note, ClinicalNoteChangeType change, string? reason) => new()
    {
        NoteId = note.Id,
        Revision = note.Revision,
        Subjective = note.Subjective,
        Objective = note.Objective,
        Assessment = note.Assessment,
        Plan = note.Plan,
        Diagnoses = note.Diagnoses.OrderBy(d => d.SortOrder).Select(d => new RevisionDiagnosis(d.Code, d.Display, d.IsPrimary)).ToList(),
        ChangeType = change,
        AmendmentReason = reason,
        ChangedBy = actor.RequireUserId(),
        CreatedAt = time.GetUtcNow(),
    };

    private static void EnsureVersion(ClinicalNote note, uint? version)
    {
        if (version is { } expected && expected != note.Version)
        {
            throw new ConflictException("concurrency_conflict", "This note was changed elsewhere. Reload it and apply your change again.")
            {
                Extensions = { ["version"] = note.Version },
            };
        }
    }

    private static bool Differs(string current, string? proposed) => proposed is not null && proposed != current;

    private static RecordAccessRequest Access(ClinicalNote note, RecordAccessAction action) =>
        new(RecordResourceType.ClinicalNote, note.Id, note.PatientId, action, note.DoctorId);

    private static NotFoundException NotFound() => new("Clinical note not found.");
}
