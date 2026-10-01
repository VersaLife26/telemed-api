using System.Net;
using System.Net.Http.Headers;
using TeleMed.Application.Admin.Credentialing;
using TeleMed.Application.DoctorApplications;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests.Infrastructure;

public static class DoctorFlows
{
    public const string Password = "doctor password";

    // A complete, decodable 1x1 PNG, so it also renders inside prescription PDFs.
    public static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x60, 0x60, 0x60, 0xF8, 0x0F,
        0x00, 0x01, 0x04, 0x01, 0x00, 0x5F, 0xE5, 0xC3, 0x4B, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
        0x44, 0xAE, 0x42, 0x60, 0x82,
    ];

    public static readonly byte[] Pdf = "%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF\n"u8.ToArray();

    public static readonly byte[] Mp4 =
    [
        0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D,
        0x00, 0x00, 0x00, 0x00, 0x69, 0x73, 0x6F, 0x6D, 0x6D, 0x70, 0x34, 0x31,
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed record ApplicationSpec
    {
        public string Phone { get; init; } = "+94771000001";
        public string Email { get; init; } = "nimal.perera@example.com";
        public string? Password { get; init; } = DoctorFlows.Password;
        public string SlmcNumber { get; init; } = "12345";
        public string SpecialtyCode { get; init; } = "general_practice";
        public string FirstName { get; init; } = "Nimal";
        public string LastName { get; init; } = "Perera";
        public string? DisplayName { get; init; }
        public string Bio { get; init; } = "Family physician.";
        public string[] Languages { get; init; } = ["en", "si"];
        public string? LanguageOther { get; init; }
        public long FeeCents { get; init; } = 250_000;
        public int ExperienceYears { get; init; } = 10;
        public bool TermsAccepted { get; init; } = true;
        public string AccountNumber { get; init; } = "0012345678";

        public object Body() => new
        {
            phone = Phone,
            email = Email,
            password = Password,
            firstName = FirstName,
            lastName = LastName,
            displayName = DisplayName,
            slmcNumber = SlmcNumber,
            specialtyCode = SpecialtyCode,
            languages = Languages,
            languageOther = LanguageOther,
            experienceYears = ExperienceYears,
            feeCents = FeeCents,
            bio = Bio,
            pgimBoardCertified = false,
            isGeneralPractitioner = true,
            medicalSchool = "University of Colombo",
            qualificationsText = "MBBS (Colombo)",
            availabilityNotes = "Weekday evenings",
            practicingLocations = new[] { "Colombo 07" },
            termsAccepted = TermsAccepted,
            bank = new { bankName = "Bank of Ceylon", branchName = "Colombo Fort", accountNumber = AccountNumber, accountName = "N. Perera" },
        };
    }

    public static Task<HttpResponseMessage> ApplyAsync(this TeleMedApiFactory factory, ApplicationSpec spec) =>
        factory.CreateClient().PostJsonAsync("/api/v1/doctor-applications", spec.Body());

    public static async Task<DoctorApplicationCreatedDto> SubmitApplicationAsync(this TeleMedApiFactory factory, ApplicationSpec? spec = null) =>
        await (await factory.ApplyAsync(spec ?? new ApplicationSpec())).ReadAsync<DoctorApplicationCreatedDto>(HttpStatusCode.Created);

    public static Task<HttpResponseMessage> UploadApplicationDocumentAsync(
        this TeleMedApiFactory factory, Guid applicationId, string type, string? uploadToken, byte[] bytes, string fileName = "scan.png")
    {
        var client = factory.CreateClient();
        if (uploadToken is not null)
        {
            client.DefaultRequestHeaders.Add("X-Upload-Token", uploadToken);
        }

        return client.PutAsync($"/api/v1/doctor-applications/{applicationId}/documents/{type}", File(bytes, fileName), Ct);
    }

    public static async Task<DoctorApplicationDto> ApproveAsync(this HttpClient admin, Guid applicationId) =>
        await (await admin.PostAsync($"/api/v1/admin/doctor-applications/{applicationId}/approve", null, Ct)).ReadAsync<DoctorApplicationDto>();

    public static async Task<(Guid DoctorId, HttpClient Doctor)> ApprovedDoctorAsync(this TeleMedApiFactory factory, ApplicationSpec? spec = null)
    {
        spec ??= new ApplicationSpec();
        var created = await factory.SubmitApplicationAsync(spec);
        var approved = await (await factory.AdminClientAsync(AdminRole.Admin)).ApproveAsync(created.Id);
        var login = await factory.CreateClient().PostJsonAsync("/api/v1/auth/login/email", new { email = spec.Email, password = spec.Password });
        var auth = await login.ReadAsync<TeleMed.Application.Auth.AuthResponse>();
        return (approved.DoctorId!.Value, factory.CreateClient().WithBearer(auth.AccessToken));
    }

    public static async Task<DoctorProfileDto> ProfileAsync(this HttpClient doctor) =>
        await (await doctor.GetAsync("/api/v1/doctors/me", Ct)).ReadAsync<DoctorProfileDto>();

    public static MultipartFormDataContent File(byte[] bytes, string fileName = "scan.png", string contentType = "application/octet-stream")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}
