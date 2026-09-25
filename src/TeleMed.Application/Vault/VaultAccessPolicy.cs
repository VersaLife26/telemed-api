using TeleMed.Application.Abstractions;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Vault;

public sealed record RecordAccessRequest(
    RecordResourceType ResourceType, Guid? ResourceId, Guid OwnerId, RecordAccessAction Action, Guid? AuthorDoctorId = null);

// Decides who may touch a patient's records and writes every decision, granted or denied, to record_access_logs.
// The patient has full access; the doctor who wrote a note or prescription may read it; a treating doctor may read and upload
// while TreatingAccess holds; nobody else, administrators included, gets anything.
public sealed class VaultAccessPolicy(
    ICurrentActor actor,
    IDoctorRepository doctors,
    IRecordAccessRepository records,
    IRequestContext request,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public const string OwnerReason = "owner";
    public const string AuthorReason = "author";
    public const string TreatingDoctorReason = "treating_doctor";
    public const string PublicVerifyReason = "public_verify";
    public const string AdminReviewReason = "admin_review";

    private const int MaxUserAgentLength = 512;

    private static readonly HashSet<RecordAccessAction> Reads = [RecordAccessAction.View, RecordAccessAction.Download, RecordAccessAction.List];

    // Throws after the denial is saved, so a refused request still leaves its row.
    public async Task AuthorizeAsync(RecordAccessRequest access, CancellationToken ct)
    {
        var (granted, reason) = await DecideAsync(access, ct);
        Record(access, granted, reason);
        await unitOfWork.SaveChangesAsync(ct);
        if (!granted)
        {
            throw new ForbiddenException("You do not have access to this record.", "record_access_denied");
        }
    }

    // Stages a row in the caller's unit of work, for decisions made elsewhere.
    public void Record(RecordAccessRequest access, bool granted, string reason) =>
        Record(access.ResourceType, access.ResourceId, access.OwnerId, access.Action, granted, reason);

    public void Record(RecordResourceType resourceType, Guid? resourceId, Guid? ownerId, RecordAccessAction action, bool granted, string reason) =>
        records.Add(new RecordAccessLog
        {
            ResourceType = resourceType,
            ResourceId = resourceId,
            OwnerId = ownerId,
            ActorId = actor.Admin?.Id ?? actor.UserId,
            ActorRole = actor.Admin is not null ? RecordActorRole.Admin
                : actor.Role switch
                {
                    UserRole.Patient => RecordActorRole.Patient,
                    UserRole.Doctor => RecordActorRole.Doctor,
                    _ => RecordActorRole.Anonymous,
                },
            Action = action,
            Granted = granted,
            Reason = reason,
            Ip = request.IpAddress,
            UserAgent = request.UserAgent is { Length: > MaxUserAgentLength } ua ? ua[..MaxUserAgentLength] : request.UserAgent,
            CreatedAt = time.GetUtcNow(),
        });

    private async Task<(bool Granted, string Reason)> DecideAsync(RecordAccessRequest access, CancellationToken ct)
    {
        if (actor.Admin is not null)
        {
            return (false, "denied:admin_no_clinical_access");
        }

        if (actor.UserId is not { } userId)
        {
            return (false, "denied:anonymous");
        }

        if (actor.Role == UserRole.Patient)
        {
            return userId == access.OwnerId ? (true, OwnerReason) : (false, "denied:not_owner");
        }

        if (actor.Role != UserRole.Doctor || await doctors.FindByUserIdAsync(userId, ct) is not { } doctor)
        {
            return (false, "denied:unsupported_role");
        }

        var read = Reads.Contains(access.Action);
        if (read && access.AuthorDoctorId == doctor.Id)
        {
            return (true, AuthorReason);
        }

        // Filing a result into the chart is the only write a treating relationship carries; deleting or reorganising stays with the patient.
        if (!read && !(access.Action == RecordAccessAction.Upload && access.ResourceType == RecordResourceType.VaultDocument))
        {
            return (false, "denied:read_only_grant");
        }

        return await IsTreatingAsync(doctor.Id, access.OwnerId, ct) ? (true, TreatingDoctorReason) : (false, "denied:no_relationship");
    }

    private async Task<bool> IsTreatingAsync(Guid doctorId, Guid patientId, CancellationToken ct) =>
        await records.FindLatestTreatingAsync(doctorId, patientId, ct) is { } latest
        && TreatingAccess.Grants(latest.StartedAt, latest.EndedAt, time.GetUtcNow());
}
