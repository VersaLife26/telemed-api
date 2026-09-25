namespace TeleMed.Api.IntegrationTests.Infrastructure;

public abstract class IntegrationTest(ApiFixture fixture) : IAsyncLifetime
{
    protected ApiFixture Fixture { get; } = fixture;
    protected TeleMedApiFactory Factory => Fixture.Factory;

    public async ValueTask InitializeAsync() => await Fixture.ResetDatabaseAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
