using TeleMed.Application.Abstractions;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.DoctorApplications;
using TeleMed.Application.Doctors;
using TeleMed.Application.Notifications;
using TeleMed.Application.Notifications.Templates;
using TeleMed.Application.Vault;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Admin.Credentialing;

public sealed class CredentialingService(
    ICurrentActor actor,
    IDoctorApplicationRepository applications,
    IDoctorDocumentRepository documents,
    IDoctorRepository doctors,
    IUserAccounts accounts,
    IFileStorage storage,
    INotificationService notifications,
    VaultAccessPolicy access,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public async Task<PagedResult<DoctorApplicationSummaryDto>> ListAsync(DoctorApplicationQuery query, CancellationToken ct)
    {
        var (items, total) = await applications.ListAsync(query.Status, query.Skip, query.PageSize, ct);
        return new PagedResult<DoctorApplicationSummaryDto>(items.Select(a => a.ToSummaryDto()).ToList(), query.Page, query.PageSize, total);
    }

    public async Task<DoctorApplicationDto> GetAsync(Guid id, CancellationToken ct) => await ToDtoAsync(await LoadAsync(id, ct), ct);

    public async Task<SignedUrlDto> GetDocumentUrlAsync(Guid id, Guid documentId, CancellationToken ct)
    {
        var document = await documents.FindLiveAsync(documentId, ct);
        if (document is null || document.ApplicationId != id)
        {
            throw new NotFoundException("Document not found.");
        }

        var owner = document.DoctorId is { } doctorId ? (await doctors.FindAsync(doctorId, ct))?.UserId : null;
        access.Record(RecordResourceType.DoctorDocument, document.Id, owner, RecordAccessAction.View, granted: true, VaultAccessPolicy.AdminReviewReason);
        await unitOfWork.SaveChangesAsync(ct);
        return new SignedUrlDto(storage.CreateSignedUrl(document.StorageKey, document.ContentType));
    }

    public async Task<DoctorApplicationDto> UpdateChecklistAsync(Guid id, UpdateChecklistRequest request, CancellationToken ct)
    {
        var application = await LoadOpenAsync(id, ct);
        var admin = actor.RequireAdmin();
        var now = time.GetUtcNow();
        ChecklistItem? Set(bool? ok, ChecklistItem? current) => ok is { } value ? new ChecklistItem(value, admin.Id, now) : current;

        var checklist = application.Checklist;
        application.Checklist = new VerificationChecklist(
            Set(request.SlmcFormat, checklist.SlmcFormat),
            Set(request.SlmcRegistry, checklist.SlmcRegistry),
            Set(request.Experience, checklist.Experience),
            Set(request.NicMatch, checklist.NicMatch),
            Set(request.PhotoClarity, checklist.PhotoClarity));
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(application, ct);
    }

    public async Task<DoctorApplicationDto> StartReviewAsync(Guid id, CancellationToken ct)
    {
        var application = await LoadAsync(id, ct);
        if (application.Status != DoctorApplicationStatus.Pending)
        {
            throw NotInStatus(application);
        }

        application.Status = DoctorApplicationStatus.UnderReview;
        application.ReviewStartedAt = time.GetUtcNow();
        application.ReviewStartedBy = actor.RequireAdmin().Id;
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(application, ct);
    }

    public async Task<DoctorApplicationDto> ApproveAsync(Guid id, CancellationToken ct)
    {
        var admin = actor.RequireAdmin();
        var application = await LoadOpenAsync(id, ct);
        if (await IsLiveAsync(accounts.FindByPhoneAsync(application.Phone, ct)) || await IsLiveAsync(accounts.FindByEmailAsync(application.Email, ct)))
        {
            throw new ConflictException(
                "applicant_account_exists",
                "An existing user account already uses this application's phone number or email, so a doctor account cannot be created for it.");
        }

        var now = time.GetUtcNow();
        var user = new User
        {
            Role = UserRole.Doctor,
            FullName = $"{application.FirstName} {application.LastName}",
            PhoneNumber = application.Phone,
            Email = application.Email,
            PasswordHash = application.PasswordHash,
        };
        await accounts.CreateAsync(user, password: null);

        var doctor = new Doctor
        {
            UserId = user.Id,
            ApplicationId = application.Id,
            SlmcNumber = application.SlmcNumber,
            SpecialtyCode = application.SpecialtyCode,
            DisplayName = application.DisplayName,
            Bio = application.Bio,
            Languages = application.Languages.ToList(),
            LanguageOther = application.LanguageOther,
            Qualifications = [new Qualification(application.QualificationsText, application.MedicalSchool, null)],
            ExperienceYears = application.ExperienceYears,
            FeeCents = application.FeeCents,
            BankName = application.BankName,
            BankBranch = application.BankBranch,
            BankAccountEncrypted = application.BankAccountEncrypted,
            ApprovedAt = now,
            ApprovedBy = admin.Id,
        };
        doctors.Add(doctor);

        var applicationDocuments = await documents.ListLiveForApplicationAsync(application.Id, ct);
        foreach (var document in applicationDocuments)
        {
            document.DoctorId = doctor.Id;
        }

        application.Status = DoctorApplicationStatus.Approved;
        application.DecidedAt = now;
        application.DecidedBy = admin.Id;
        application.DoctorId = doctor.Id;
        await notifications.EnqueueAsync(
            new NotificationRecipient(application.Email, null, user.Language, user.Id),
            DoctorApplicationModel.Approved(application.DisplayName),
            $"application:{application.Id}:approved",
            ct);
        await unitOfWork.SaveChangesAsync(ct);
        return application.ToDto(applicationDocuments);
    }

    public async Task<DoctorApplicationDto> RejectAsync(Guid id, RejectApplicationRequest request, CancellationToken ct)
    {
        var application = await LoadOpenAsync(id, ct);
        var reason = request.Reason.Trim();
        application.Status = DoctorApplicationStatus.Rejected;
        application.RejectionReason = reason;
        application.DecidedAt = time.GetUtcNow();
        application.DecidedBy = actor.RequireAdmin().Id;
        await notifications.EnqueueAsync(
            new NotificationRecipient(application.Email, null, Language.En),
            DoctorApplicationModel.Rejected(application.DisplayName, reason),
            $"application:{application.Id}:rejected",
            ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await ToDtoAsync(application, ct);
    }

    private static async Task<bool> IsLiveAsync(Task<User?> lookup) => await lookup is { Status: not UserStatus.Deleted };

    private async Task<DoctorApplicationDto> ToDtoAsync(DoctorApplication application, CancellationToken ct) =>
        application.ToDto(await documents.ListLiveForApplicationAsync(application.Id, ct));

    private async Task<DoctorApplication> LoadAsync(Guid id, CancellationToken ct) =>
        await applications.FindAsync(id, ct) ?? throw new NotFoundException("Application not found.");

    private async Task<DoctorApplication> LoadOpenAsync(Guid id, CancellationToken ct)
    {
        var application = await LoadAsync(id, ct);
        return application.IsOpen ? application : throw NotInStatus(application);
    }

    private static ConflictException NotInStatus(DoctorApplication application) =>
        new("invalid_application_status", $"The application is {application.Status}; this action is not allowed.");
}
