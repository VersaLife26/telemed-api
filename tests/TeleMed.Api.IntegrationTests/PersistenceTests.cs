using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Persistence;

namespace TeleMed.Api.IntegrationTests;

public class PersistenceTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task All_migrations_are_applied()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var applied = await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        applied.ShouldBe(db.Database.GetMigrations());
        (await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Btree_gist_extension_is_installed()
    {
        await using var connection = await Fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM pg_extension WHERE extname = 'btree_gist'";

        ((long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!).ShouldBe(1);
    }

    [Fact]
    public async Task Saving_stamps_timestamps_and_stores_enums_as_snake_case()
    {
        var user = NewUser("a@example.com");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Add(user);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        user.CreatedAt.ShouldBe(Factory.Time.GetUtcNow());
        user.UpdatedAt.ShouldBe(user.CreatedAt);

        await using var connection = await Fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT role || '/' || status || '/' || language FROM users";
        ((string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!).ShouldBe("doctor/active/si");
    }

    [Fact]
    public async Task Unique_violation_becomes_conflict()
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Add(NewUser("dup@example.com"));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var second = Factory.Services.CreateAsyncScope();
        second.ServiceProvider.GetRequiredService<AppDbContext>().Users.Add(NewUser("dup@example.com"));
        var ex = await Should.ThrowAsync<ConflictException>(
            second.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(TestContext.Current.CancellationToken));

        ex.Code.ShouldBe("email_taken");
    }

    private static User NewUser(string email) => new()
    {
        UserName = email,
        NormalizedUserName = Guid.NewGuid().ToString(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        FullName = "Test User",
        Role = UserRole.Doctor,
        Language = Language.Si,
    };
}
