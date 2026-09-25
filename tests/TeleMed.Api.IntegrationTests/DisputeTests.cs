using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.Disputes;
using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.FinanceFlows;

namespace TeleMed.Api.IntegrationTests;

public class DisputeTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_dispute_is_opened_worked_resolved_with_a_refund_request_and_closed()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var appointment = await Factory.CapturedAsync(patient, doctor.DoctorId, 9);
        var supportAdmin = await Factory.CreateAdminAsync(AdminRole.Support);
        var support = Factory.CreateAdminClient(Factory.LocalAdminToken(supportAdmin.Email));
        var colleague = await Factory.CreateAdminAsync(AdminRole.Ops);

        var opened = await (await support.PostJsonAsync("/api/v1/admin/disputes",
            new { appointmentId = appointment.Id, subject = "Doctor left early", description = "The call ended after five minutes." }))
            .ReadAsync<DisputeDetailDto>(HttpStatusCode.Created);
        var id = opened.Dispute.Id;
        opened.Dispute.Status.ShouldBe(DisputeStatus.Open);
        opened.Dispute.PatientId.ShouldBe(patient.UserId);
        opened.Dispute.DoctorId.ShouldBe(doctor.DoctorId);
        opened.Dispute.OpenedByAdminId.ShouldBe(supportAdmin.Id);

        (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/close", new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var claimed = await (await support.PostAsync($"/api/v1/admin/disputes/{id}/assign", null, Ct)).ReadAsync<DisputeDetailDto>();
        claimed.Dispute.Status.ShouldBe(DisputeStatus.Investigating);
        claimed.Dispute.AssignedAdminId.ShouldBe(supportAdmin.Id);
        var reassigned = await (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/assign", new { adminUserId = colleague.Id }))
            .ReadAsync<DisputeDetailDto>();
        reassigned.Dispute.AssignedAdminId.ShouldBe(colleague.Id);
        (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/assign", new { adminUserId = Guid.NewGuid() })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var comment = await (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/comments", new { body = "Called the patient." }))
            .ReadAsync<DisputeCommentDto>(HttpStatusCode.Created);
        comment.AuthorAdminId.ShouldBe(supportAdmin.Id);

        (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/resolve", new { resolution = "Too much", refundAmountCents = Fee + 1 }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var resolved = await (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/resolve", new { resolution = "Half refund agreed", refundAmountCents = 125_000 }))
            .ReadAsync<DisputeDetailDto>();
        resolved.Dispute.Status.ShouldBe(DisputeStatus.Resolved);
        resolved.Dispute.Resolution.ShouldBe("Half refund agreed");
        resolved.Comments.ShouldHaveSingleItem().Body.ShouldBe("Called the patient.");
        var refund = resolved.Refunds.ShouldHaveSingleItem();
        refund.Status.ShouldBe(RefundStatus.Requested);
        refund.Reason.ShouldBe(RefundReason.Dispute);
        refund.DisputeId.ShouldBe(id);
        refund.AmountCents.ShouldBe(125_000);
        refund.Percent.ShouldBe(50);

        (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/resolve", new { resolution = "Again" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var closed = await (await support.PostAsync($"/api/v1/admin/disputes/{id}/close", null, Ct)).ReadAsync<DisputeDetailDto>();
        closed.Dispute.Status.ShouldBe(DisputeStatus.Closed);
        closed.Dispute.ClosedAt.ShouldNotBeNull();

        var finance = await Factory.AdminClientAsync(AdminRole.Finance);
        var pending = await (await finance.GetAsync("/api/v1/admin/finance/refunds?status=requested", Ct)).ReadAsync<PagedResult<AdminRefundDto>>();
        pending.Items.ShouldHaveSingleItem().Id.ShouldBe(refund.Id);

        var list = await (await support.GetAsync("/api/v1/admin/disputes?status=closed", Ct)).ReadAsync<PagedResult<DisputeDto>>();
        list.Items.ShouldHaveSingleItem().Id.ShouldBe(id);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM audit_logs WHERE entity_type = 'disputes' AND entity_id = '{id}'")).ShouldBeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task Resolving_without_a_refund_needs_no_payment_and_unknown_appointments_are_not_found()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var booked = await patient.Client.BookedAsync(doctor.DoctorId, await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3)));
        var admin = await Factory.AdminClientAsync(AdminRole.Admin);

        (await admin.PostJsonAsync("/api/v1/admin/disputes", new { appointmentId = Guid.NewGuid(), subject = "x", description = "y" })).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        var opened = await (await admin.PostJsonAsync("/api/v1/admin/disputes", new { appointmentId = booked.Id, subject = "Rude", description = "Complaint" }))
            .ReadAsync<DisputeDetailDto>(HttpStatusCode.Created);

        var resolved = await (await admin.PostJsonAsync($"/api/v1/admin/disputes/{opened.Dispute.Id}/resolve", new { resolution = "Apologised" }))
            .ReadAsync<DisputeDetailDto>();
        resolved.Dispute.Status.ShouldBe(DisputeStatus.Resolved);
        resolved.Refunds.ShouldBeEmpty();
    }
}
