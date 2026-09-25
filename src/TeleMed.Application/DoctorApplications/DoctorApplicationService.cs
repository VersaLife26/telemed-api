using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Admin.Notifications;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Notifications;
using TeleMed.Application.Notifications.Templates;
using TeleMed.Application.Reference;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.DoctorApplications;

public sealed class DoctorApplicationService(
    IDoctorApplicationRepository applications,
    IDoctorDocumentRepository documents,
    IDoctorRepository doctors,
    IReferenceDataRepository reference,
    IUserAccounts accounts,
    IBankDataCipher cipher,
    IFileStorage storage,
    INotificationService notifications,
    AdminNotificationService adminInbox,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    private static readonly HashSet<DoctorDocumentType> UploadableTypes =
    [
        DoctorDocumentType.SlmcCertificate,
        DoctorDocumentType.Signature,
        DoctorDocumentType.Seal,
        DoctorDocumentType.Nic,
        DoctorDocumentType.DegreeCertificate,
    ];

    public async Task<EligibilityDto> GetEligibilityAsync(EligibilityQuery query, CancellationToken ct)
    {
        var phone = PhoneNumber.NormalizeSriLankanMobile(query.Phone)!;
        if (await doctors.FindByUserPhoneAsync(phone, ct) is { } doctor)
        {
            return new EligibilityDto(ApplicantEligibility.Doctor, doctor.ApplicationId, doctor.Id);
        }

        if (await applications.FindLatestByPhoneAsync(phone, ct) is not { } application)
        {
            return new EligibilityDto(ApplicantEligibility.None, null, null);
        }

        var status = application.Status switch
        {
            DoctorApplicationStatus.Pending => ApplicantEligibility.Pending,
            DoctorApplicationStatus.UnderReview => ApplicantEligibility.UnderReview,
            DoctorApplicationStatus.Approved => ApplicantEligibility.Approved,
            _ => ApplicantEligibility.Rejected,
        };
        return new EligibilityDto(status, application.Id, application.DoctorId);
    }

    public async Task<DoctorApplicationCreatedDto> ApplyAsync(DoctorApplicationRequest request, CancellationToken ct)
    {
        var phone = PhoneNumber.NormalizeSriLankanMobile(request.Phone)!;
        var slmcNumber = SlmcNumbers.Normalize(request.SlmcNumber);
        if (!await reference.IsActiveSpecialtyAsync(request.SpecialtyCode, ct))
        {
            throw DoctorFiles.Invalid("Unknown specialty.", "specialtyCode");
        }

        if (await doctors.SlmcNumberExistsAsync(slmcNumber, ct))
        {
            throw new ConflictException("slmc_registered", "A doctor with this SLMC number is already registered.");
        }

        if (await doctors.FindByUserPhoneAsync(phone, ct) is not null)
        {
            throw new ConflictException("already_doctor", "This phone number already belongs to a registered doctor.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await IsLiveAsync(accounts.FindByPhoneAsync(phone, ct)) || await IsLiveAsync(accounts.FindByEmailAsync(email, ct)))
        {
            throw new ConflictException("account_exists", "An account already uses this phone number or email. Use different contact details to apply.");
        }

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        var bankAccount = new BankAccount(request.Bank.AccountNumber.Trim(), request.Bank.AccountName.Trim());
        var uploadToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var application = new DoctorApplication
        {
            Phone = phone,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? $"{firstName} {lastName}" : request.DisplayName.Trim(),
            SlmcNumber = slmcNumber,
            SpecialtyCode = request.SpecialtyCode,
            Languages = request.Languages.ToList(),
            LanguageOther = string.IsNullOrWhiteSpace(request.LanguageOther) ? null : request.LanguageOther.Trim(),
            ExperienceYears = request.ExperienceYears,
            FeeCents = request.FeeCents,
            Bio = request.Bio?.Trim() ?? "",
            PgimBoardCertified = request.PgimBoardCertified,
            IsGeneralPractitioner = request.IsGeneralPractitioner,
            MedicalSchool = request.MedicalSchool.Trim(),
            QualificationsText = request.QualificationsText.Trim(),
            AvailabilityNotes = request.AvailabilityNotes.Trim(),
            PracticingLocations = request.PracticingLocations.Select(l => l.Trim()).ToList(),
            TermsAcceptedAt = time.GetUtcNow(),
            BankName = request.Bank.BankName.Trim(),
            BankBranch = request.Bank.BranchName.Trim(),
            BankAccountEncrypted = cipher.Encrypt(JsonSerializer.Serialize(bankAccount, JsonSerializerOptions.Web)),
            PasswordHash = request.Password is null ? null : accounts.HashPassword(request.Password),
            UploadTokenHash = HashToken(uploadToken),
        };
        applications.Add(application);
        await notifications.EnqueueAsync(
            new NotificationRecipient(application.Email, null, Language.En),
            DoctorApplicationModel.Submitted(application.DisplayName),
            $"application:{application.Id}:submitted",
            ct);
        adminInbox.Add(
            AdminNotificationKind.DoctorApplicationSubmitted,
            "New doctor application",
            $"{application.DisplayName} (SLMC {application.SlmcNumber}, {application.SpecialtyCode}) applied for verification.",
            $"/doctor-applications/{application.Id}",
            application.Id);
        await unitOfWork.SaveChangesAsync(ct);
        return new DoctorApplicationCreatedDto(application.Id, uploadToken);
    }

    public async Task<DoctorDocumentDto> UploadDocumentAsync(
        Guid id, string type, string? uploadToken, Stream content, string? fileName, CancellationToken ct)
    {
        var documentType = DocumentTypes.Parse(type);
        if (!UploadableTypes.Contains(documentType))
        {
            throw DoctorFiles.Invalid("This document type cannot be attached to an application.", "type");
        }

        var application = await applications.FindAsync(id, ct) ?? throw new NotFoundException("Application not found.");
        if (uploadToken is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(HashToken(uploadToken)), Encoding.ASCII.GetBytes(application.UploadTokenHash)))
        {
            throw new UnauthorizedException("invalid_upload_token", "The upload token is missing or invalid.");
        }

        if (application.Status != DoctorApplicationStatus.Pending)
        {
            throw new ConflictException("application_not_pending", "Documents can only be changed while the application is pending.");
        }

        var allowed = documentType is DoctorDocumentType.Signature or DoctorDocumentType.Seal ? DoctorFiles.Stamps : DoctorFiles.Documents;
        var upload = await DoctorFiles.ReadAsync(content, PlatformPolicy.DoctorDocumentMaxBytes, allowed, ct);
        var document = await DoctorFiles.StoreAsync(storage, $"doctor-applications/{application.Id}", application.Id, null, documentType, upload, fileName, ct);

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        if (await documents.FindLiveForApplicationAsync(application.Id, documentType, ct) is { } previous)
        {
            previous.DeletedAt = time.GetUtcNow();
            // The unique index on live rows per type is filtered, so EF cannot order the retire before the insert itself.
            await unitOfWork.SaveChangesAsync(ct);
        }

        documents.Add(document);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return document.ToDto();
    }

    private static async Task<bool> IsLiveAsync(Task<User?> lookup) => await lookup is { Status: not UserStatus.Deleted };

    private static string HashToken(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
