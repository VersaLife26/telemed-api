using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TeleMed.Application.Admin.AdminUsers;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Auth;
using TeleMed.Application.Testing;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;

namespace TeleMed.Api.IntegrationTests.Infrastructure;

public static partial class Api
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, Json, Ct);

    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, object body) =>
        client.PatchAsJsonAsync(url, body, Json, Ct);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, Json, Ct);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.ShouldBe(expected, body);
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        return JsonDocument.Parse(body).RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static HttpClient WithBearer(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static WebApplicationFactory<Program> WithSettings(this TeleMedApiFactory factory, params (string Key, string? Value)[] settings) =>
        factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, s.Value)))));

    public static HttpClient CreateTestInboxClient(this TeleMedApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Secret", TeleMedApiFactory.TestSecret);
        return client;
    }

    public static async Task<string> LatestCodeAsync(this TeleMedApiFactory factory, string recipient)
    {
        var response = await factory.CreateTestInboxClient()
            .GetAsync($"/api/v1/test/captured-messages/latest?recipient={Uri.EscapeDataString(recipient)}", Ct);
        var message = await response.ReadAsync<CapturedMessageDto>();
        return SixDigits().Match(message.Body).Value;
    }

    public static async Task<AuthResponse> SignInWithPhoneAsync(this TeleMedApiFactory factory, string phone = "+94771234567")
    {
        var client = factory.CreateClient();
        (await client.PostJsonAsync("/api/v1/auth/otp/send", new { phone })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var code = await factory.LatestCodeAsync(phone);
        return await (await client.PostJsonAsync("/api/v1/auth/otp/verify", new { phone, code })).ReadAsync<AuthResponse>();
    }

    public static async Task<AuthResponse> CreateUserAsync(this TeleMedApiFactory factory, UserRole role, string? email = null, string? password = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = new User { Role = role, FullName = $"Test {role}", Email = email ?? $"{Guid.NewGuid():N}@example.com" };
        await scope.ServiceProvider.GetRequiredService<IUserAccounts>().CreateAsync(user, password);
        var session = scope.ServiceProvider.GetRequiredService<SessionIssuer>().Issue(user);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return session.Response;
    }

    public static async Task<AdminUser> CreateAdminAsync(this WebApplicationFactory<Program> factory, AdminRole role, bool isActive = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        // Unique emails keep the process-wide admin cache from leaking identities between tests.
        var admin = new AdminUser
        {
            Email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@admin.test",
            DisplayName = $"Test {role}",
            Role = role,
            IsActive = isActive,
        };
        scope.ServiceProvider.GetRequiredService<IAdminUserRepository>().Add(admin);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return admin;
    }

    public static string LocalAdminToken(this TeleMedApiFactory factory, string email, string signingKey = TeleMedApiFactory.AdminSigningKey)
    {
        var now = factory.Time.GetUtcNow().UtcDateTime;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "telemed-admin-local",
            Audience = "telemed-admin",
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddHours(1),
            Claims = new Dictionary<string, object> { ["email"] = email },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256),
        });
    }

    public static HttpClient CreateAdminClient(this WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient().WithBearer(token);
        client.DefaultRequestHeaders.Add("Origin", TeleMedApiFactory.AdminOrigin);
        return client;
    }

    public static async Task<HttpClient> AdminClientAsync(this TeleMedApiFactory factory, AdminRole role)
    {
        var admin = await factory.CreateAdminAsync(role);
        return factory.CreateAdminClient(factory.LocalAdminToken(admin.Email));
    }

    public static async Task<T?> ScalarAsync<T>(this ApiFixture fixture, string sql)
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(Ct);
        return value is null or DBNull ? default : (T)value;
    }

    public static async Task ExecuteSqlAsync(this ApiFixture fixture, string sql)
    {
        await using var connection = await fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Ct);
    }

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();
}
