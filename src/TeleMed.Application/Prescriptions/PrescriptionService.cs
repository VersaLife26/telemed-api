using FluentValidation;
using FluentValidation.Results;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Consultations;
using TeleMed.Application.Doctors;
using TeleMed.Application.Notifications;
using TeleMed.Application.Notifications.Templates;
using TeleMed.Application.Reference;
using TeleMed.Application.Scheduling;
using TeleMed.Application.Vault;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Prescriptions;

public sealed class PrescriptionService(
    ICurrentActor actor,
    IAppointmentRepository appointments,
    IDoctorRepository doctors,
    IDoctorDocumentRepository doctorDocuments,
    IConsultationRepository consultations,
    IPrescriptionRepository prescriptions,
    IReferenceDataRepository reference,
    IPrescriptionSigner signer,
    IPrescriptionPdfRenderer renderer,
    IFileStorage storage,
    INotificationService notifications,
    IAppLinks links,
    VaultAccessPolicy policy,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    // Prescribing needs the treating doctor, a consultation that started, and the doctor's signature and seal on file;
    // authority to prescribe from a consultation lapses with the treating-access window.
    public async Task<PrescriptionDto> IssueAsync(Guid appointmentId, IssuePrescriptionRequest request, CancellationToken ct)
    {
        var doctor = await doctors.MineWritableAsync(actor, ct);
        var appointment = await appointments.FindAsync(appointmentId, ct);
        if (appointment is null || appointment.DoctorId != doctor.Id)
        {
            throw new NotFoundException("Appointment not found.");
        }

        var now = time.GetUtcNow();
        var consultation = await consultations.FindByAppointmentAsync(appointment.Id, ct);
        if (consultation?.StartedAt is not { } startedAt)
        {
            throw new ConflictException("consultation_not_started", "A prescription can be issued once the consultation has started.");
        }

        if (!TreatingAccess.Grants(startedAt, consultation.EndedAt, now))
        {
            throw new ConflictException("treating_window_closed", "This consultation is too old to prescribe against; see the patient again.");
        }

        if (await prescriptions.FindByAppointmentAsync(appointment.Id, ct) is not null)
        {
            throw new ConflictException("prescription_exists", "A prescription has already been issued for this appointment.");
        }

        if (await doctorDocuments.FindLiveForDoctorAsync(doctor.Id, DoctorDocumentType.Signature, ct) is null
            || await doctorDocuments.FindLiveForDoctorAsync(doctor.Id, DoctorDocumentType.Seal, ct) is null)
        {
            throw new ConflictException("stamps_required", "Upload your signature and seal in your profile before issuing a prescription.");
        }

        var drugIds = request.Items.Where(i => i.DrugId is not null).Select(i => i.DrugId!.Value).Distinct().ToList();
        var drugs = drugIds.Count == 0 ? [] : (await reference.FindDrugsAsync(drugIds, ct)).ToDictionary(d => d.Id);
        var unknown = request.Items.Select((item, i) => (item, i)).Where(x => x.item.DrugId is { } id && !drugs.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
        {
            throw new ValidationException(unknown.Select(x => new ValidationFailure($"items[{x.i}].drugId", "Unknown drug.")));
        }

        var id = Guid.CreateVersion7();
        var prescription = new Prescription
        {
            Id = id,
            AppointmentId = appointment.Id,
            DoctorId = doctor.Id,
            PatientId = appointment.PatientId,
            DoctorName = doctor.DisplayName,
            DoctorSlmc = doctor.SlmcNumber,
            DoctorQualifications = string.Join('\n', doctor.Qualifications.Select(q =>
                q.Year is { } year ? $"{q.Degree} ({q.Institution}, {year})" : $"{q.Degree} ({q.Institution})")),
            IssuedAt = PrescriptionSignature.TruncateToMicroseconds(now),
            IsTest = appointment.IsTest,
            Items = request.Items.Select((item, i) =>
            {
                var drug = item.DrugId is { } drugId ? drugs[drugId] : null;
                return new PrescriptionItem
                {
                    PrescriptionId = id,
                    DrugId = item.DrugId,
                    DrugName = item.DrugName.Trim(),
                    Strength = Blank(item.Strength) ? drug?.Strength ?? "" : item.Strength!.Trim(),
                    Form = Blank(item.Form) ? drug?.Form ?? "" : item.Form!.Trim(),
                    Dosage = item.Dosage.Trim(),
                    Frequency = item.Frequency.Trim(),
                    DurationDays = item.DurationDays,
                    Quantity = item.Quantity,
                    Instructions = Blank(item.Instructions) ? null : item.Instructions!.Trim(),
                    IsGeneric = item.IsGeneric,
                    SortOrder = i,
                };
            }).ToList(),
            Investigations = (request.Investigations ?? [])
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList(),
        };
        prescription.VerificationHmac = signer.Sign(prescription);
        prescriptions.Add(prescription);

        if (!appointment.IsTest)
        {
            var pdf = await RenderPdfCoreAsync(prescription, appointment, ct);
            await notifications.EnqueueAsync(
                appointment.PatientId,
                new PrescriptionReadyModel(doctor.DisplayName, links.Prescription(id)),
                $"rx:{id}:ready",
                ct,
                new EmailAttachment(pdf.FileName, pdf.Content));
        }

        await unitOfWork.SaveChangesAsync(ct);
        return prescription.ToDto();
    }

    public async Task<PrescriptionDto> GetForAppointmentAsync(Guid appointmentId, CancellationToken ct)
    {
        var prescription = await prescriptions.FindByAppointmentAsync(appointmentId, ct) ?? throw NotFound();
        await policy.AuthorizeAsync(Access(prescription, RecordAccessAction.View), ct);
        return prescription.ToDto();
    }

    public async Task<PrescriptionDto> GetAsync(Guid id, CancellationToken ct)
    {
        var prescription = await prescriptions.FindAsync(id, ct) ?? throw NotFound();
        await policy.AuthorizeAsync(Access(prescription, RecordAccessAction.View), ct);
        return prescription.ToDto();
    }

    public async Task<PagedResult<PrescriptionDto>> ListMineAsync(PrescriptionQuery query, CancellationToken ct)
    {
        var (items, total) = actor.Role == UserRole.Doctor
            ? await prescriptions.ListAsync(null, (await doctors.MineAsync(actor, ct)).Id, query.Skip, query.PageSize, ct)
            : await prescriptions.ListAsync(actor.RequireUserId(), null, query.Skip, query.PageSize, ct);
        return new PagedResult<PrescriptionDto>(items.Select(p => p.ToDto()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<PrescriptionPdfFile> RenderPdfAsync(Guid id, CancellationToken ct)
    {
        var prescription = await prescriptions.FindAsync(id, ct) ?? throw NotFound();
        await policy.AuthorizeAsync(Access(prescription, RecordAccessAction.Download), ct);

        var appointment = await appointments.FindAsync(prescription.AppointmentId, ct) ?? throw NotFound();
        return await RenderPdfCoreAsync(prescription, appointment, ct);
    }

    private async Task<PrescriptionPdfFile> RenderPdfCoreAsync(Prescription prescription, Appointment appointment, CancellationToken ct)
    {
        var issuedOn = SlotPlanner.LocalDate(prescription.IssuedAt, IanaTimeZone.Find(PlatformPolicy.TimeZoneId));
        var pdf = new PrescriptionPdf(
            prescription.Id,
            prescription.IssuedAt,
            prescription.DoctorName,
            prescription.DoctorSlmc,
            prescription.DoctorQualifications,
            appointment.VisitPatientName,
            AgeOn(appointment.VisitPatientDateOfBirth, issuedOn),
            appointment.VisitPatientSex?.ToString(),
            appointment.VisitPatientWeightKg,
            appointment.VisitPatientAllergies,
            prescription.Items.OrderBy(i => i.SortOrder)
                .Select(i => new PrescriptionPdfItem(i.DrugName, i.Strength, i.Form, i.Dosage, i.Frequency, i.DurationDays, i.Quantity, i.Instructions, i.IsGeneric))
                .ToList(),
            prescription.Investigations,
            await StampAsync(prescription.DoctorId, DoctorDocumentType.Signature, ct),
            await StampAsync(prescription.DoctorId, DoctorDocumentType.Seal, ct),
            signer.VerifyUrl(prescription.Id, prescription.VerificationHmac),
            prescription.IsTest);
        return new PrescriptionPdfFile(renderer.Render(pdf), $"prescription-{prescription.Id}.pdf");
    }

    public async Task<PrescriptionDto> CancelAsync(Guid id, CancelPrescriptionRequest? request, CancellationToken ct)
    {
        var doctor = await doctors.MineWritableAsync(actor, ct);
        var prescription = await prescriptions.FindForUpdateAsync(id, ct);
        if (prescription is null || prescription.DoctorId != doctor.Id)
        {
            throw NotFound();
        }

        if (prescription.Status == PrescriptionStatus.Cancelled)
        {
            throw new ConflictException("prescription_cancelled", "This prescription has already been cancelled.");
        }

        prescription.Status = PrescriptionStatus.Cancelled;
        prescription.CancelledAt = time.GetUtcNow();
        prescription.CancellationReason = Blank(request?.Reason) ? null : request!.Reason!.Trim();
        await unitOfWork.SaveChangesAsync(ct);
        return prescription.ToDto();
    }

    // Recomputes the HMAC over the current rows and compares it with the value from the QR code, so an edited row fails.
    // An unknown id answers exactly like a wrong signature and leaves no log row, so the endpoint is not an existence oracle.
    public async Task<PrescriptionVerificationDto> VerifyAsync(Guid id, string? hmac, CancellationToken ct)
    {
        if (await prescriptions.FindAsync(id, ct) is not { } prescription)
        {
            return PrescriptionVerificationDto.Invalid;
        }

        var reason = !signer.Verify(prescription, hmac) ? PrescriptionVerificationDto.InvalidReason
            : prescription.IsTest ? PrescriptionVerificationDto.TestReason
            : prescription.Status == PrescriptionStatus.Cancelled ? PrescriptionVerificationDto.CancelledReason
            : null;
        policy.Record(Access(prescription, RecordAccessAction.Verify), reason is null, reason is null ? VaultAccessPolicy.PublicVerifyReason : $"denied:{reason}");
        await unitOfWork.SaveChangesAsync(ct);

        if (reason == PrescriptionVerificationDto.InvalidReason)
        {
            return PrescriptionVerificationDto.Invalid;
        }

        var appointment = await appointments.FindAsync(prescription.AppointmentId, ct);
        return new PrescriptionVerificationDto(
            reason is null,
            reason,
            prescription.IssuedAt,
            prescription.DoctorName,
            prescription.DoctorSlmc,
            Initials(appointment?.VisitPatientName),
            prescription.Items.OrderBy(i => i.SortOrder).Select(i => i.ToVerifiedDto()).ToList(),
            prescription.Investigations);
    }

    private async Task<byte[]?> StampAsync(Guid doctorId, DoctorDocumentType type, CancellationToken ct)
    {
        if (await doctorDocuments.FindLiveForDoctorAsync(doctorId, type, ct) is not { } document || storage.OpenRead(document.StorageKey) is not { } stream)
        {
            return null;
        }

        await using (stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return buffer.ToArray();
        }
    }

    private static int? AgeOn(DateOnly birthDate, DateOnly on)
    {
        var age = on.Year - birthDate.Year - (on < birthDate.AddYears(on.Year - birthDate.Year) ? 1 : 0);
        return age >= 0 ? age : null;
    }

    private static string? Initials(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? null
            : string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + "."));

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    private static RecordAccessRequest Access(Prescription prescription, RecordAccessAction action) =>
        new(RecordResourceType.Prescription, prescription.Id, prescription.PatientId, action, prescription.DoctorId);

    private static NotFoundException NotFound() => new("Prescription not found.");
}
