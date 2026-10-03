using Npgsql;
using Respawn;
using Respawn.Graph;
using TeleMed.Infrastructure.Persistence.Configurations;
using TeleMed.Infrastructure.Persistence.Seed;
using Testcontainers.PostgreSql;

namespace TeleMed.Api.IntegrationTests.Infrastructure;

public sealed class ApiFixture : IAsyncLifetime
{
    private static readonly string[] AppendOnlyTables = ["clinical_note_revisions", "record_access_logs"];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private Respawner _respawner = null!;

    public TeleMedApiFactory Factory { get; private set; } = null!;

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new TeleMedApiFactory(ConnectionString);
        Factory.StartServer();

        await using var connection = await OpenConnectionAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Table("__EFMigrationsHistory"), new Table("icd10_codes"), new Table("audit_logs")],
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = string.Join('\n', AppendOnlyTables.Select(t => $"ALTER TABLE {t} DISABLE TRIGGER USER;"));
        await command.ExecuteNonQueryAsync();
        await _respawner.ResetAsync(connection);
        command.CommandText = string.Join('\n',
            SeedSql.Read("specialties.sql"),
            SeedSql.Read("drugs.sql"),
            // Respawn truncates the singleton. Each test starts with no exchange rate.
            $"INSERT INTO platform_billing_settings (id, lkr_per_usd, created_at, updated_at) VALUES ('{PlatformBillingSettingsConfiguration.SingletonId}', NULL, TIMESTAMPTZ '2026-10-03 00:00:00+00', TIMESTAMPTZ '2026-10-03 00:00:00+00');",
            "ALTER TABLE audit_logs DISABLE TRIGGER USER;",
            "TRUNCATE audit_logs RESTART IDENTITY;",
            "ALTER TABLE audit_logs ENABLE TRIGGER USER;",
            string.Join('\n', AppendOnlyTables.Select(t => $"ALTER TABLE {t} ENABLE TRIGGER USER;")));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
