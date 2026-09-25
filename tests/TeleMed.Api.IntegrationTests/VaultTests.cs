using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Common;
using TeleMed.Application.Vault;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;
using static TeleMed.Api.IntegrationTests.Infrastructure.ClinicalFlows;

namespace TeleMed.Api.IntegrationTests;

public class VaultTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private const string Documents = "/api/v1/vault/documents";
    private const string Folders = "/api/v1/vault/folders";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_patient_uploads_organises_downloads_and_deletes_their_own_documents()
    {
        var patient = (await Factory.PatientAsync()).Client;

        var document = await UploadedAsync(patient, DoctorFlows.Pdf, "Blood report.pdf", "report");
        document.ContentType.ShouldBe(FileSignature.Pdf);
        document.SizeBytes.ShouldBe(DoctorFlows.Pdf.Length);
        document.DocumentType.ShouldBe(VaultDocumentType.Report);
        var folder = await (await patient.PostJsonAsync(Folders, new { name = "Labs" })).ReadAsync<VaultFolderDto>(HttpStatusCode.Created);
        (await (await patient.PostJsonAsync(Folders, new { name = " labs " })).ProblemCodeAsync()).ShouldBe("folder_exists");
        (await patient.PostJsonAsync(Folders, new { name = "a/b" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var moved = await (await patient.PatchJsonAsync($"{Documents}/{document.Id}", new { fileName = "CBC", folderId = folder.Id.ToString() }))
            .ReadAsync<VaultDocumentDto>();
        moved.FileName.ShouldBe("CBC.pdf");
        moved.FolderId.ShouldBe(folder.Id);
        (await (await patient.PatchJsonAsync($"{Documents}/{document.Id}", new { fileName = "cbc-final.PDF" })).ReadAsync<VaultDocumentDto>())
            .FileName.ShouldBe("cbc-final.PDF");
        (await ListAsync(patient, "?folderId=root")).Total.ShouldBe(0);
        (await ListAsync(patient, $"?folderId={folder.Id}")).Items.ShouldHaveSingleItem().Id.ShouldBe(document.Id);
        (await ListAsync(patient, "?documentType=scan")).Total.ShouldBe(0);
        (await patient.GetAsync($"{Documents}?folderId=nowhere", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var download = await (await patient.GetAsync($"{Documents}/{document.Id}/download", Ct)).ReadAsync<VaultDownloadDto>();
        download.FileName.ShouldBe("cbc-final.PDF");
        download.ExpiresInSeconds.ShouldBe(300);
        (await (await Factory.CreateClient().GetAsync(download.Url, Ct)).Content.ReadAsByteArrayAsync(Ct)).ShouldBe(DoctorFlows.Pdf);

        (await (await patient.DeleteAsync($"{Folders}/{folder.Id}", Ct)).ProblemCodeAsync()).ShouldBe("folder_not_empty");
        (await patient.DeleteAsync($"{Documents}/{document.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await patient.GetAsync($"{Documents}/{document.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListAsync(patient, "")).Total.ShouldBe(0);
        (await patient.DeleteAsync($"{Folders}/{folder.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await (await patient.GetAsync(Folders, Ct)).ReadAsync<List<VaultFolderDto>>()).ShouldBeEmpty();
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM vault_documents WHERE id = '{document.Id}' AND deleted_at IS NOT NULL")).ShouldBe(1);
    }

    [Fact]
    public async Task Folders_cannot_be_moved_inside_themselves()
    {
        var patient = (await Factory.PatientAsync()).Client;
        var parent = await (await patient.PostJsonAsync(Folders, new { name = "2026" })).ReadAsync<VaultFolderDto>(HttpStatusCode.Created);
        var child = await (await patient.PostJsonAsync(Folders, new { name = "September", parentId = parent.Id })).ReadAsync<VaultFolderDto>(HttpStatusCode.Created);

        (await (await patient.PatchJsonAsync($"{Folders}/{parent.Id}", new { parentId = child.Id.ToString() })).ProblemCodeAsync()).ShouldBe("folder_cycle");
        (await (await patient.PatchJsonAsync($"{Folders}/{parent.Id}", new { parentId = parent.Id.ToString() })).ProblemCodeAsync()).ShouldBe("folder_cycle");
        (await (await patient.DeleteAsync($"{Folders}/{parent.Id}", Ct)).ProblemCodeAsync()).ShouldBe("folder_not_empty");

        var moved = await (await patient.PatchJsonAsync($"{Folders}/{child.Id}", new { parentId = "root", name = "Sept" })).ReadAsync<VaultFolderDto>();
        moved.ParentId.ShouldBeNull();
        moved.Name.ShouldBe("Sept");
        (await (await Factory.PatientAsync()).Client.DeleteAsync($"{Folders}/{parent.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Uploads_are_capped_at_10_MB_and_must_really_be_the_type_their_extension_claims()
    {
        var patient = (await Factory.PatientAsync()).Client;
        var limit = (int)PlatformPolicy.VaultMaxDocumentBytes;

        (await UploadAsync(patient, PdfOfSize(limit), "max.pdf")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await UploadAsync(patient, PdfOfSize(limit + 1), "big.pdf")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await UploadAsync(patient, DoctorFlows.Png, "scan.pdf")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await UploadAsync(patient, DoctorFlows.Png, "scan")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await UploadAsync(patient, "just some text"u8.ToArray(), "notes.txt")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await UploadAsync(patient, DoctorFlows.Png, "scan.PNG", "scan")).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ListAsync(patient, "")).Total.ShouldBe(2);
    }

    [Fact]
    public async Task A_treating_doctor_reads_and_files_documents_until_the_window_closes_and_every_decision_is_logged()
    {
        var (doctor, patient, appointmentId) = await Factory.StartedInstantMeetingAsync();
        var document = await UploadedAsync(patient.Client, DoctorFlows.Pdf, "report.pdf");
        var ended = await doctor.Client.EndedAsync(appointmentId);
        var outsider = await Factory.BookableDoctorAsync(SecondDoctor);

        (await ListAsync(doctor.Client, $"?patientId={patient.UserId}")).Items.ShouldHaveSingleItem().Id.ShouldBe(document.Id);
        (await doctor.Client.GetAsync(Documents, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await doctor.Client.GetAsync($"{Documents}/{document.Id}/download", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var filed = await UploadedAsync(doctor.Client, DoctorFlows.Png, "xray.png", "scan", patient.UserId);
        filed.OwnerId.ShouldBe(patient.UserId);
        filed.UploadedBy.ShouldBe(doctor.UserId);
        (await doctor.Client.DeleteAsync($"{Documents}/{document.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await doctor.Client.PatchJsonAsync($"{Documents}/{document.Id}", new { fileName = "mine" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var accessible = await (await doctor.Client.GetAsync("/api/v1/vault/patients", Ct)).ReadAsync<List<AccessiblePatientDto>>();
        accessible.ShouldHaveSingleItem().PatientId.ShouldBe(patient.UserId);
        accessible[0].AccessExpiresAt.ShouldBe(ended.EndedAt!.Value + PlatformPolicy.TreatingAccessWindow, TimeSpan.FromMilliseconds(1));
        (await outsider.Client.GetAsync($"{Documents}/{document.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await (await Factory.PatientAsync()).Client.GetAsync($"{Documents}/{document.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        Factory.Time.Advance(ended.EndedAt!.Value + PlatformPolicy.TreatingAccessWindow - TimeSpan.FromSeconds(1) - Factory.Time.GetUtcNow());
        var lateDoctor = await Factory.ClientForAsync(doctor.UserId);
        (await lateDoctor.GetAsync($"{Documents}/{document.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        Factory.Time.Advance(TimeSpan.FromSeconds(1));
        (await lateDoctor.GetAsync($"{Documents}/{document.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await lateDoctor.GetAsync($"{Documents}?patientId={patient.UserId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await UploadAsync(lateDoctor, DoctorFlows.Png, "late.png", patientId: patient.UserId)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await (await lateDoctor.GetAsync("/api/v1/vault/patients", Ct)).ReadAsync<List<AccessiblePatientDto>>()).ShouldBeEmpty();

        var doctorLogs = $"actor_id = '{doctor.UserId}' AND resource_type = 'vault_document'";
        (await Fixture.AccessLogCountAsync($"{doctorLogs} AND granted AND reason = 'treating_doctor'")).ShouldBe(4);
        (await Fixture.AccessLogCountAsync($"{doctorLogs} AND NOT granted AND reason = 'denied:no_relationship'")).ShouldBe(3);
        (await Fixture.AccessLogCountAsync($"{doctorLogs} AND NOT granted AND reason = 'denied:read_only_grant'")).ShouldBe(2);
        (await Fixture.AccessLogCountAsync($"actor_id = '{outsider.UserId}' AND NOT granted AND owner_id = '{patient.UserId}'")).ShouldBe(1);
        (await Fixture.AccessLogCountAsync($"actor_id = '{patient.UserId}' AND action = 'upload' AND granted AND reason = 'owner'")).ShouldBe(1);
    }

    private static byte[] PdfOfSize(int size)
    {
        var bytes = new byte[size];
        "%PDF-1.4\n"u8.CopyTo(bytes);
        return bytes;
    }

    private static Task<HttpResponseMessage> UploadAsync(
        HttpClient client, byte[] bytes, string fileName, string documentType = "report", Guid? patientId = null)
    {
        var form = DoctorFlows.File(bytes, fileName);
        form.Add(new StringContent(documentType), "documentType");
        if (patientId is { } id)
        {
            form.Add(new StringContent(id.ToString()), "patientId");
        }

        return client.PostAsync(Documents, form, Ct);
    }

    private static async Task<VaultDocumentDto> UploadedAsync(
        HttpClient client, byte[] bytes, string fileName, string documentType = "report", Guid? patientId = null) =>
        await (await UploadAsync(client, bytes, fileName, documentType, patientId)).ReadAsync<VaultDocumentDto>(HttpStatusCode.Created);

    private static async Task<PagedResult<VaultDocumentDto>> ListAsync(HttpClient client, string query) =>
        await (await client.GetAsync(Documents + query, Ct)).ReadAsync<PagedResult<VaultDocumentDto>>();
}
