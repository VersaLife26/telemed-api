using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Admin.Disputes;
using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Common;
using TeleMed.Application.CustomerCare;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.FinanceFlows;

namespace TeleMed.Api.IntegrationTests;

public class DisputeTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_user_opens_customer_care_and_admins_work_the_thread_to_a_refund()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var appointment = await Factory.CapturedAsync(patient, doctor.DoctorId, 9);
        var supportAdmin = await Factory.CreateAdminAsync(AdminRole.Support);
        var support = Factory.CreateAdminClient(Factory.LocalAdminToken(supportAdmin.Email));
        var colleague = await Factory.CreateAdminAsync(AdminRole.Ops);

        var opened = await (await patient.Client.PostJsonAsync("/api/v1/customer-care",
            new { category = "refund", body = "The call ended after five minutes.", appointmentId = appointment.Id }))
            .ReadAsync<CustomerCareThreadDto>(HttpStatusCode.Created);
        var id = opened.Id;
        opened.Category.ShouldBe(DisputeCategory.Refund);
        opened.AppointmentId.ShouldBe(appointment.Id);
        opened.Messages.ShouldHaveSingleItem().FromSupport.ShouldBeFalse();

        var listed = await (await patient.Client.GetAsync("/api/v1/customer-care", Ct)).ReadAsync<PagedResult<CustomerCareSummaryDto>>();
        listed.Items.ShouldHaveSingleItem().Id.ShouldBe(id);
        (await (await Factory.PatientAsync()).Client.GetAsync($"/api/v1/customer-care/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var caseFile = await (await support.GetAsync($"/api/v1/admin/disputes/{id}", Ct)).ReadAsync<DisputeDetailDto>();
        caseFile.Dispute.Status.ShouldBe(DisputeStatus.Open);
        caseFile.Dispute.OpenedBy.ShouldBe(DisputeOpener.Patient);
        caseFile.Dispute.OpenedByUserId.ShouldBe(patient.UserId);
        caseFile.Dispute.OpenedByAdminId.ShouldBeNull();
        caseFile.Dispute.Category.ShouldBe(DisputeCategory.Refund);

        (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/close", new { })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var claimed = await (await support.PostAsync($"/api/v1/admin/disputes/{id}/assign", null, Ct)).ReadAsync<DisputeDetailDto>();
        claimed.Dispute.Status.ShouldBe(DisputeStatus.Investigating);
        claimed.Dispute.AssignedAdminId.ShouldBe(supportAdmin.Id);
        var reassigned = await (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/assign", new { adminUserId = colleague.Id }))
            .ReadAsync<DisputeDetailDto>();
        reassigned.Dispute.AssignedAdminId.ShouldBe(colleague.Id);
        (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/assign", new { adminUserId = Guid.NewGuid() })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var reply = await (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/comments", new { body = "We can refund half." }))
            .ReadAsync<DisputeCommentDto>(HttpStatusCode.Created);
        reply.AuthorAdminId.ShouldBe(supportAdmin.Id);
        reply.FromSupport.ShouldBeTrue();

        var thread = await (await patient.Client.GetAsync($"/api/v1/customer-care/{id}", Ct)).ReadAsync<CustomerCareThreadDto>();
        thread.Messages.Count.ShouldBe(2);
        thread.Messages[1].FromSupport.ShouldBeTrue();
        thread.Messages[1].Body.ShouldBe("We can refund half.");

        var followUp = await (await patient.Client.PostJsonAsync($"/api/v1/customer-care/{id}/messages", new { body = "Please do." }))
            .ReadAsync<CustomerCareMessageDto>(HttpStatusCode.Created);
        followUp.FromSupport.ShouldBeFalse();

        (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/resolve", new { resolution = "Too much", refundAmountCents = Fee + 1 }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var resolved = await (await support.PostJsonAsync($"/api/v1/admin/disputes/{id}/resolve", new { resolution = "Half refund agreed", refundAmountCents = 125_000 }))
            .ReadAsync<DisputeDetailDto>();
        resolved.Dispute.Status.ShouldBe(DisputeStatus.Resolved);
        resolved.Dispute.Resolution.ShouldBe("Half refund agreed");
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
        (await patient.Client.PostJsonAsync($"/api/v1/customer-care/{id}/messages", new { body = "Hello?" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var finance = await Factory.AdminClientAsync(AdminRole.Finance);
        var pending = await (await finance.GetAsync("/api/v1/admin/finance/refunds?status=requested", Ct)).ReadAsync<PagedResult<AdminRefundDto>>();
        pending.Items.ShouldHaveSingleItem().Id.ShouldBe(refund.Id);

        var list = await (await support.GetAsync("/api/v1/admin/disputes?status=closed&category=refund", Ct)).ReadAsync<PagedResult<DisputeDto>>();
        list.Items.ShouldHaveSingleItem().Id.ShouldBe(id);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM audit_logs WHERE entity_type = 'disputes' AND entity_id = '{id}'")).ShouldBeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task A_general_conversation_resolves_without_a_payment_and_unknown_appointments_are_not_found()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Admin);

        (await doctor.Client.PostJsonAsync("/api/v1/customer-care", new { category = "technical", body = "App crash", appointmentId = Guid.NewGuid() }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var opened = await (await doctor.Client.PostJsonAsync("/api/v1/customer-care", new { category = "technical", body = "The workspace will not load." }))
            .ReadAsync<CustomerCareThreadDto>(HttpStatusCode.Created);
        opened.AppointmentId.ShouldBeNull();

        var detail = await (await admin.GetAsync($"/api/v1/admin/disputes/{opened.Id}", Ct)).ReadAsync<DisputeDetailDto>();
        detail.Dispute.OpenedBy.ShouldBe(DisputeOpener.Doctor);

        (await admin.PostJsonAsync($"/api/v1/admin/disputes/{opened.Id}/resolve", new { resolution = "Ask them to refresh", refundAmountCents = 1_000 }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var resolved = await (await admin.PostJsonAsync($"/api/v1/admin/disputes/{opened.Id}/resolve", new { resolution = "Asked them to refresh the workspace" }))
            .ReadAsync<DisputeDetailDto>();
        resolved.Dispute.Status.ShouldBe(DisputeStatus.Resolved);
        resolved.Refunds.ShouldBeEmpty();
    }
}
