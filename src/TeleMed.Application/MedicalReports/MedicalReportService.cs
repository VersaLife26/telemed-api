using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Consultations;
using TeleMed.Application.Doctors;
using TeleMed.Application.Notifications;
using TeleMed.Application.Notifications.Templates;
using TeleMed.Application.Scheduling;
using TeleMed.Application.Vault;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.MedicalReports;

public sealed class MedicalReportService(
    ICurrentActor actor,
    IAppointmentRepository appointments,
    IDoctorRepository doctors,
    IDoctorDocumentRepository doctorDocuments,
    IConsultationRepository consultations,
    IMedicalReportRepository reports,
    IMedicalReportSigner signer,
    IMedicalReportPdfRenderer renderer,
    IFileStorage storage,
    INotificationService notifications,
    IAppLinks links,
    VaultAccessPolicy policy,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public async Task<MedicalReportDto> IssueAsync(Guid appointmentId, IssueMedicalReportRequest request, CancellationToken ct)
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
            throw new ConflictException("consultation_not_started", "A medical report can be issued once the consultation has started.");
        }

        if (!TreatingAccess.Grants(startedAt, consultation.EndedAt, now))
        {
            throw new ConflictException("treating_window_closed", "This consultation is too old to issue a report against; see the patient again.");
        }

        if (await reports.FindByAppointmentAsync(appointment.Id, ct) is not null)
        {
            throw new ConflictException("medical_report_exists", "A medical report has already been issued for this appointment.");
        }

        if (await doctorDocuments.FindLiveForDoctorAsync(doctor.Id, DoctorDocumentType.Signature, ct) is null
            || await doctorDocuments.FindLiveForDoctorAsync(doctor.Id, DoctorDocumentType.Seal, ct) is null)
        {
            throw new ConflictException("stamps_required", "Upload your signature and seal in your profile before issuing a medical report.");
        }

        var id = Guid.CreateVersion7();
        var report = new MedicalReport
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
            Addressee = Blank(request.Addressee) ? MedicalReport.DefaultAddressee : request.Addressee!.Trim(),
            ClinicalImpression = request.ClinicalImpression.Trim(),
            Findings = Blank(request.Findings) ? null : request.Findings!.Trim(),
            Advice = Blank(request.Advice) ? null : request.Advice!.Trim(),
            Fitness = request.Fitness,
            LeaveFrom = request.LeaveFrom,
            LeaveUntil = request.LeaveUntil,
            ReturnToWorkOn = request.ReturnToWorkOn,
            FitnessNotes = Blank(request.FitnessNotes) ? null : request.FitnessNotes!.Trim(),
        };
        report.VerificationHmac = signer.Sign(report);
        reports.Add(report);

        if (!appointment.IsTest)
        {
            var pdf = await RenderPdfCoreAsync(report, appointment, ct);
            await notifications.EnqueueAsync(
                appointment.PatientId,
                new MedicalReportReadyModel(doctor.DisplayName, links.MedicalReport(id)),
                $"mr:{id}:ready",
                ct,
                new EmailAttachment(pdf.FileName, pdf.Content));
        }

        await unitOfWork.SaveChangesAsync(ct);
        return report.ToDto();
    }

    public async Task<MedicalReportDto> GetForAppointmentAsync(Guid appointmentId, CancellationToken ct)
    {
        var report = await reports.FindByAppointmentAsync(appointmentId, ct) ?? throw NotFound();
        await policy.AuthorizeAsync(Access(report, RecordAccessAction.View), ct);
        return report.ToDto();
    }

    public async Task<MedicalReportDto> GetAsync(Guid id, CancellationToken ct)
    {
        var report = await reports.FindAsync(id, ct) ?? throw NotFound();
        await policy.AuthorizeAsync(Access(report, RecordAccessAction.View), ct);
        return report.ToDto();
    }

    public async Task<PagedResult<MedicalReportDto>> ListMineAsync(MedicalReportQuery query, CancellationToken ct)
    {
        var (items, total) = actor.Role == UserRole.Doctor
            ? await reports.ListAsync(null, (await doctors.MineAsync(actor, ct)).Id, query.Skip, query.PageSize, ct)
            : await reports.ListAsync(actor.RequireUserId(), null, query.Skip, query.PageSize, ct);
        return new PagedResult<MedicalReportDto>(items.Select(r => r.ToDto()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<MedicalReportPdfFile> RenderPdfAsync(Guid id, CancellationToken ct)
    {
        var report = await reports.FindAsync(id, ct) ?? throw NotFound();
        await policy.AuthorizeAsync(Access(report, RecordAccessAction.Download), ct);

        var appointment = await appointments.FindAsync(report.AppointmentId, ct) ?? throw NotFound();
        return await RenderPdfCoreAsync(report, appointment, ct);
    }

    private async Task<MedicalReportPdfFile> RenderPdfCoreAsync(MedicalReport report, Appointment appointment, CancellationToken ct)
    {
        var zone = IanaTimeZone.Find(PlatformPolicy.TimeZoneId);
        var issuedOn = SlotPlanner.LocalDate(report.IssuedAt, zone);
        var visitOn = SlotPlanner.LocalDate(appointment.StartAt, zone);
        var pdf = new MedicalReportPdf(
            report.Id,
            report.IssuedAt,
            visitOn,
            report.DoctorName,
            report.DoctorSlmc,
            report.DoctorQualifications,
            appointment.VisitPatientName,
            AgeOn(appointment.VisitPatientDateOfBirth, issuedOn),
            appointment.VisitPatientSex?.ToString(),
            report.Addressee,
            report.ClinicalImpression,
            report.Findings,
            report.Advice,
            report.Fitness,
            report.LeaveFrom,
            report.LeaveUntil,
            report.ReturnToWorkOn,
            report.FitnessNotes,
            await StampAsync(report.DoctorId, DoctorDocumentType.Signature, ct),
            await StampAsync(report.DoctorId, DoctorDocumentType.Seal, ct),
            signer.VerifyUrl(report.Id, report.VerificationHmac),
            report.IsTest);
        return new MedicalReportPdfFile(renderer.Render(pdf), $"medical-report-{report.Id}.pdf");
    }

    public async Task<MedicalReportDto> CancelAsync(Guid id, CancelMedicalReportRequest? request, CancellationToken ct)
    {
        var doctor = await doctors.MineWritableAsync(actor, ct);
        var report = await reports.FindForUpdateAsync(id, ct);
        if (report is null || report.DoctorId != doctor.Id)
        {
            throw NotFound();
        }

        if (report.Status == MedicalReportStatus.Cancelled)
        {
            throw new ConflictException("medical_report_cancelled", "This medical report has already been cancelled.");
        }

        report.Status = MedicalReportStatus.Cancelled;
        report.CancelledAt = time.GetUtcNow();
        report.CancellationReason = Blank(request?.Reason) ? null : request!.Reason!.Trim();
        await unitOfWork.SaveChangesAsync(ct);
        return report.ToDto();
    }

    public async Task<MedicalReportVerificationDto> VerifyAsync(Guid id, string? hmac, CancellationToken ct)
    {
        if (await reports.FindAsync(id, ct) is not { } report)
        {
            return MedicalReportVerificationDto.Invalid;
        }

        var reason = !signer.Verify(report, hmac) ? MedicalReportVerificationDto.InvalidReason
            : report.IsTest ? MedicalReportVerificationDto.TestReason
            : report.Status == MedicalReportStatus.Cancelled ? MedicalReportVerificationDto.CancelledReason
            : null;
        policy.Record(Access(report, RecordAccessAction.Verify), reason is null, reason is null ? VaultAccessPolicy.PublicVerifyReason : $"denied:{reason}");
        await unitOfWork.SaveChangesAsync(ct);

        if (reason == MedicalReportVerificationDto.InvalidReason)
        {
            return MedicalReportVerificationDto.Invalid;
        }

        var appointment = await appointments.FindAsync(report.AppointmentId, ct);
        return new MedicalReportVerificationDto(
            reason is null,
            reason,
            report.IssuedAt,
            report.DoctorName,
            report.DoctorSlmc,
            Initials(appointment?.VisitPatientName),
            report.ClinicalImpression,
            report.Fitness);
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

    private static RecordAccessRequest Access(MedicalReport report, RecordAccessAction action) =>
        new(RecordResourceType.MedicalReport, report.Id, report.PatientId, action, report.DoctorId);

    private static NotFoundException NotFound() => new("Medical report not found.");
}
