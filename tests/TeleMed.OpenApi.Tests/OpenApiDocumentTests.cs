using System.Text.Json.Nodes;
using TeleMed.OpenApi.Tests.Infrastructure;

namespace TeleMed.OpenApi.Tests;

public class OpenApiDocumentTests : IAsyncLifetime, IAsyncDisposable
{
    public const string UpdateVariable = "UPDATE_OPENAPI";

    private readonly OpenApiExportFactory _factory = new();

    private static string CheckedInPath
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TeleMed.slnx")))
            {
                dir = dir.Parent;
            }

            return Path.Combine(dir!.FullName, "openapi", "v1.json");
        }
    }

    public ValueTask InitializeAsync()
    {
        _factory.StartServer();
        return ValueTask.CompletedTask;
    }

    private async Task<string> ServedAsync()
    {
        var json = await _factory.CreateClient().GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        return JsonNode.Parse(json)!.ToJsonString(new() { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";
    }

    // Regenerate with: UPDATE_OPENAPI=1 dotnet test --project tests/TeleMed.OpenApi.Tests --filter-class "*OpenApiDocumentTests"
    [Fact]
    public async Task Checked_in_openapi_document_matches_the_served_one()
    {
        var served = await ServedAsync();
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            await File.WriteAllTextAsync(CheckedInPath, served, TestContext.Current.CancellationToken);
        }

        File.Exists(CheckedInPath).ShouldBeTrue($"openapi/v1.json is missing; run with {UpdateVariable}=1 to create it.");
        (await File.ReadAllTextAsync(CheckedInPath, TestContext.Current.CancellationToken)).ShouldBe(
            served, $"openapi/v1.json is stale; run with {UpdateVariable}=1 to regenerate it.");
    }

    [Fact]
    public async Task Document_has_operation_ids_security_problem_responses_and_string_enums()
    {
        var document = JsonNode.Parse(await ServedAsync())!;
        var operations = document["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject().Select(op => (Key: $"{op.Key} {path.Key}", Operation: op.Value!)))
            .ToList();

        var ids = operations.Select(o => (string?)o.Operation["operationId"]).ToList();
        ids.ShouldAllBe(id => !string.IsNullOrEmpty(id));
        ids.Distinct().Count().ShouldBe(ids.Count);

        document["components"]!["securitySchemes"]!.AsObject().Select(s => s.Key)
            .ShouldBe(["userBearer", "adminCloudflareAccess", "adminLocalJwt", "testSecret"], ignoreOrder: true);
        operations.Where(o => o.Key.Contains("/api/v1/admin/", StringComparison.Ordinal))
            .ShouldAllBe(o => o.Operation["security"]!.AsArray().Count == 2);

        var book = operations.Single(o => o.Key == "post /api/v1/appointments").Operation;
        book["security"]![0]!["userBearer"].ShouldNotBeNull();
        book["responses"]!["201"]!["content"]!["application/json"].ShouldNotBeNull();
        foreach (var status in new[] { "400", "401", "403", "409" })
        {
            ((string?)book["responses"]![status]!["content"]!["application/problem+json"]!["schema"]!["$ref"]).ShouldBe("#/components/schemas/ProblemDetails");
        }

        var enums = document["components"]!["schemas"]!.AsObject().Where(s => s.Value!["enum"] is not null).ToList();
        enums.ShouldNotBeEmpty();
        enums.ShouldAllBe(s => (string?)s.Value!["type"] == "string" && s.Value!["enum"]!.AsArray().All(v => v != null && v.GetValueKind() == System.Text.Json.JsonValueKind.String));
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();
}
