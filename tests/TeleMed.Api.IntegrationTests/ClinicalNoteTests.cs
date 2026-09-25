using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.ClinicalNotes;
using TeleMed.Application.Common;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;
using static TeleMed.Api.IntegrationTests.Infrastructure.ClinicalFlows;

namespace TeleMed.Api.IntegrationTests;

public class ClinicalNoteTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_draft_is_finalised_then_amended_with_every_revision_kept_and_the_patient_sees_only_the_signed_note()
    {
        var (doctor, patient, id) = await Factory.StartedInstantMeetingAsync();

        var draft = await (await doctor.Client.PutJsonAsync(NoteUrl(id), new
        {
            subjective = "Fever for two days",
            assessment = "Viral fever",
            diagnoses = new[] { new { code = "r50.9", isPrimary = true } },
        })).ReadAsync<ClinicalNoteDto>();
        draft.Status.ShouldBe(ClinicalNoteStatus.Draft);
        draft.Revision.ShouldBe(0);
        draft.Diagnoses.ShouldHaveSingleItem().Code.ShouldBe("R50.9");
        (await patient.Client.GetAsync(NoteUrl(id), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        draft = await (await doctor.Client.PutJsonAsync(NoteUrl(id), new
        {
            subjective = "Fever for two days",
            assessment = "Viral fever",
            plan = "Rest and fluids",
            diagnoses = new[] { new { code = "R50.9", isPrimary = true } },
            version = draft.Version,
        })).ReadAsync<ClinicalNoteDto>();
        var stale = await doctor.Client.PutJsonAsync(NoteUrl(id), new { subjective = "x", version = draft.Version + 1 });
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await stale.ProblemCodeAsync()).ShouldBe("concurrency_conflict");

        var finalised = await (await doctor.Client.PostJsonAsync(NoteUrl(id, "/finalise"), new { version = draft.Version })).ReadAsync<ClinicalNoteDto>();
        finalised.Status.ShouldBe(ClinicalNoteStatus.Finalised);
        finalised.Revision.ShouldBe(1);
        finalised.FinalisedAt.ShouldNotBeNull();
        (await (await doctor.Client.PutJsonAsync(NoteUrl(id), new { subjective = "rewrite" })).ProblemCodeAsync()).ShouldBe("note_finalised");
        (await (await doctor.Client.PostAsync(NoteUrl(id, "/finalise"), null, Ct)).ProblemCodeAsync()).ShouldBe("note_finalised");

        (await doctor.Client.PostJsonAsync(NoteUrl(id, "/amend"), new { reason = "", plan = "x" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await (await doctor.Client.PostJsonAsync(NoteUrl(id, "/amend"), new { reason = "Nothing", plan = "Rest and fluids" })).ProblemCodeAsync())
            .ShouldBe("no_change");
        var amended = await (await doctor.Client.PostJsonAsync(NoteUrl(id, "/amend"), new
        {
            reason = "Added the dengue test",
            plan = "Rest, fluids and an NS1 test",
            diagnoses = new[] { new { code = "R50.9", isPrimary = false }, new { code = "A90", isPrimary = true } },
        })).ReadAsync<ClinicalNoteDto>();
        amended.Revision.ShouldBe(2);
        amended.Subjective.ShouldBe("Fever for two days");
        amended.Diagnoses.Select(d => (d.Code, d.IsPrimary)).ShouldBe([("R50.9", false), ("A90", true)]);

        var seen = await (await patient.Client.GetAsync(NoteUrl(id), Ct)).ReadAsync<ClinicalNoteDto>();
        seen.Plan.ShouldBe("Rest, fluids and an NS1 test");
        (await patient.Client.GetAsync(NoteUrl(id, "/revisions"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var revisions = await (await doctor.Client.GetAsync(NoteUrl(id, "/revisions"), Ct)).ReadAsync<List<ClinicalNoteRevisionDto>>();
        revisions.Select(r => (r.Revision, r.ChangeType, r.AmendmentReason)).ShouldBe(
        [
            (1, ClinicalNoteChangeType.Finalise, null),
            (2, ClinicalNoteChangeType.Amend, "Added the dengue test"),
        ]);
        revisions[0].Plan.ShouldBe("Rest and fluids");
        revisions[0].Diagnoses.ShouldHaveSingleItem().IsPrimary.ShouldBeTrue();

        var mine = await (await doctor.Client.GetAsync("/api/v1/clinical-notes?status=finalised", Ct)).ReadAsync<PagedResult<ClinicalNoteSummaryDto>>();
        mine.Items.ShouldHaveSingleItem().AppointmentId.ShouldBe(id);
        (await Fixture.AccessLogCountAsync($"resource_type = 'clinical_note' AND action IN ('finalise', 'amend') AND granted")).ShouldBe(2);
        (await Fixture.AccessLogCountAsync($"actor_id = '{patient.UserId}' AND action = 'view' AND granted = false AND reason = 'denied:draft_not_author'"))
            .ShouldBe(1);
        (await Fixture.AccessLogCountAsync($"actor_id = '{patient.UserId}' AND action = 'view' AND granted AND reason = 'owner'")).ShouldBe(1);
    }

    [Fact]
    public async Task Revisions_are_append_only_in_the_database()
    {
        var (doctor, _, id) = await Factory.StartedInstantMeetingAsync();
        await doctor.Client.PutJsonAsync(NoteUrl(id), new { subjective = "Cough" });
        await doctor.Client.PostAsync(NoteUrl(id, "/finalise"), null, Ct);

        await Should.ThrowAsync<Npgsql.PostgresException>(() => Fixture.ExecuteSqlAsync("UPDATE clinical_note_revisions SET plan = 'forged'"));
        await Should.ThrowAsync<Npgsql.PostgresException>(() => Fixture.ExecuteSqlAsync("DELETE FROM clinical_note_revisions"));
        await Should.ThrowAsync<Npgsql.PostgresException>(() => Fixture.ExecuteSqlAsync("DELETE FROM record_access_logs"));
    }

    [Fact]
    public async Task Only_the_treating_doctor_writes_and_other_doctors_and_admins_read_nothing()
    {
        var (doctor, patient, id) = await Factory.StartedInstantMeetingAsync();
        await doctor.Client.PutJsonAsync(NoteUrl(id), new { subjective = "Cough" });
        await doctor.Client.PostAsync(NoteUrl(id, "/finalise"), null, Ct);
        var other = await Factory.BookableDoctorAsync(SecondDoctor);

        (await other.Client.PutJsonAsync(NoteUrl(id), new { subjective = "x" })).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var denied = await other.Client.GetAsync(NoteUrl(id), Ct);
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await denied.ProblemCodeAsync()).ShouldBe("record_access_denied");
        (await (await Factory.PatientAsync()).Client.GetAsync(NoteUrl(id), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var admin = await Factory.AdminClientAsync(AdminRole.SuperAdmin);
        (await admin.GetAsync(NoteUrl(id), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await Fixture.AccessLogCountAsync($"actor_id = '{other.UserId}' AND granted = false AND reason = 'denied:no_relationship'")).ShouldBe(1);
        (await Fixture.AccessLogCountAsync($"owner_id = '{patient.UserId}' AND granted = false")).ShouldBe(2);
    }

    [Fact]
    public async Task Diagnoses_must_be_known_unique_codes_with_at_most_one_primary()
    {
        var (doctor, _, id) = await Factory.StartedInstantMeetingAsync();

        async Task<HttpResponseMessage> SaveAsync(params object[] diagnoses) => await doctor.Client.PutJsonAsync(NoteUrl(id), new { subjective = "x", diagnoses });

        (await SaveAsync(new { code = "ZZZ.9", isPrimary = false })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SaveAsync(new { code = "A90", isPrimary = false }, new { code = "a90 ", isPrimary = false })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SaveAsync(new { code = "A90", isPrimary = true }, new { code = "R50.9", isPrimary = true })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SaveAsync(Enumerable.Range(0, 11).Select(i => (object)new { code = $"A0{i}", isPrimary = false }).ToArray())).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await SaveAsync(new { code = "A90", isPrimary = true }, new { code = "R50.9", isPrimary = false })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var swapped = await (await SaveAsync(new { code = "R50.9", isPrimary = true }, new { code = "A90", isPrimary = false })).ReadAsync<ClinicalNoteDto>();
        swapped.Diagnoses.Select(d => (d.Code, d.IsPrimary)).ShouldBe([("R50.9", true), ("A90", false)]);
    }

    [Fact]
    public async Task Notes_wait_for_the_consultation_to_start()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var id = await patient.Client.InstantMeetingAsync(new { counterpartUserId = doctor.UserId });

        var early = await doctor.Client.PutJsonAsync(NoteUrl(id), new { subjective = "x" });

        early.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await early.ProblemCodeAsync()).ShouldBe("consultation_not_started");
        (await doctor.Client.PostAsync(NoteUrl(id, "/finalise"), null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
