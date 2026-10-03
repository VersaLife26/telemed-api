using TeleMed.Application.Abstractions;
using TeleMed.Application.Admin.Disputes;
using TeleMed.Application.Admin.Notifications;
using TeleMed.Application.Appointments;
using TeleMed.Application.Notifications;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Doctors;
using TeleMed.Application.Notifications.Templates;
using TeleMed.Application.Scheduling;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.CustomerCare;

public sealed class CustomerCareService(
    ICurrentActor actor,
    IDisputeRepository disputes,
    IAppointmentRepository appointments,
    IDoctorRepository doctors,
    AdminNotificationService adminInbox,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public async Task<PagedResult<CustomerCareSummaryDto>> ListAsync(CustomerCareQuery query, CancellationToken ct)
    {
        var userId = actor.RequireUserId();
        var (items, total) = await disputes.ListOpenedByAsync(userId, query.Skip, query.PageSize, ct);
        var summaries = new List<CustomerCareSummaryDto>(items.Count);
        foreach (var dispute in items)
        {
            var comments = await disputes.ListCommentsAsync(dispute.Id, ct);
            summaries.Add(ToSummary(dispute, comments));
        }

        return new PagedResult<CustomerCareSummaryDto>(summaries, query.Page, query.PageSize, total);
    }

    public async Task<CustomerCareThreadDto> GetAsync(Guid id, CancellationToken ct) =>
        ToThread(await LoadMineAsync(id, ct), await disputes.ListCommentsAsync(id, ct));

    public async Task<CustomerCareThreadDto> OpenAsync(OpenCustomerCareRequest request, CancellationToken ct)
    {
        var userId = actor.RequireUserId();
        Guid? patientId = actor.Role == UserRole.Patient ? userId : null;
        Guid? doctorId = actor.Role == UserRole.Doctor ? (await doctors.MineAsync(actor, ct)).Id : null;
        Guid? appointmentId = null;
        if (request.AppointmentId is { } requested)
        {
            var appointment = await appointments.FindAsync(requested, ct) ?? throw new NotFoundException("Appointment not found.");
            if (appointment.PatientId != userId && (doctorId is null || appointment.DoctorId != doctorId))
            {
                throw new NotFoundException("Appointment not found.");
            }

            appointmentId = appointment.Id;
            patientId = appointment.PatientId;
            doctorId = appointment.DoctorId;
        }

        var body = request.Body.Trim();
        var dispute = new Dispute
        {
            AppointmentId = appointmentId,
            PatientId = patientId,
            DoctorId = doctorId,
            Category = request.Category,
            Subject = request.Category.Subject(),
            Description = body,
            OpenedByUserId = userId,
        };
        disputes.Add(dispute);
        disputes.AddComment(new DisputeComment { DisputeId = dispute.Id, AuthorUserId = userId, Body = body });
        adminInbox.Add(
            AdminNotificationKind.CustomerCare,
            "New customer care message",
            $"{request.Category.Subject()}: {Trim(body, 160)}",
            "/disputes",
            dispute.Id);
        await unitOfWork.SaveChangesAsync(ct);
        return ToThread(dispute, await disputes.ListCommentsAsync(dispute.Id, ct));
    }

    public async Task<CustomerCareMessageDto> ReplyAsync(Guid id, CustomerCareMessageRequest request, CancellationToken ct)
    {
        var userId = actor.RequireUserId();
        var dispute = await LoadMineForUpdateAsync(id, ct);
        if (dispute.Status == DisputeStatus.Closed)
        {
            throw new ConflictException("conversation_closed", "This conversation is closed.");
        }

        var comment = new DisputeComment { DisputeId = id, AuthorUserId = userId, Body = request.Body.Trim() };
        disputes.AddComment(comment);
        dispute.UpdatedAt = time.GetUtcNow();
        adminInbox.Add(
            AdminNotificationKind.CustomerCare,
            "Customer care reply",
            $"{dispute.Subject}: {Trim(comment.Body, 160)}",
            "/disputes",
            dispute.Id);
        await unitOfWork.SaveChangesAsync(ct);
        return ToMessage(comment);
    }

    public static async Task NotifyOpenerAsync(INotificationService notifications, Dispute dispute, CancellationToken ct)
    {
        if (dispute.OpenedByUserId is not { } userId)
        {
            return;
        }

        await notifications.EnqueueAsync(
            userId,
            new CustomerCareReplyModel(dispute.Subject),
            $"care:{dispute.Id}:reply:{Guid.CreateVersion7()}",
            ct);
    }

    private async Task<Dispute> LoadMineAsync(Guid id, CancellationToken ct)
    {
        var dispute = await disputes.FindAsync(id, ct) ?? throw NotFound();
        return dispute.OpenedByUserId == actor.RequireUserId() ? dispute : throw NotFound();
    }

    private async Task<Dispute> LoadMineForUpdateAsync(Guid id, CancellationToken ct)
    {
        var dispute = await disputes.FindForUpdateAsync(id, ct) ?? throw NotFound();
        return dispute.OpenedByUserId == actor.RequireUserId() ? dispute : throw NotFound();
    }

    private static CustomerCareSummaryDto ToSummary(Dispute dispute, IReadOnlyList<DisputeComment> comments)
    {
        var last = comments.Count == 0 ? dispute.Description : comments[^1].Body;
        return new(dispute.Id, dispute.Category, dispute.Status, dispute.AppointmentId, dispute.Subject, Trim(last, 160), dispute.UpdatedAt);
    }

    private static CustomerCareThreadDto ToThread(Dispute dispute, IReadOnlyList<DisputeComment> comments) =>
        new(
            dispute.Id,
            dispute.Category,
            dispute.Status,
            dispute.AppointmentId,
            dispute.Subject,
            dispute.CreatedAt,
            dispute.UpdatedAt,
            comments.Select(ToMessage).ToList());

    private static CustomerCareMessageDto ToMessage(DisputeComment comment) =>
        new(comment.Id, comment.Body, comment.AuthorAdminId is not null, comment.CreatedAt);

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "…";

    private static NotFoundException NotFound() => new("Conversation not found.");
}
