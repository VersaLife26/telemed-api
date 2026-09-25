using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Jobs;
using TeleMed.Domain.Rules;

namespace TeleMed.Api.IntegrationTests;

public class UserErasureTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Deleted_accounts_are_anonymised_after_the_grace_period_and_their_appointments_stay()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var auth = await Factory.SignInWithPhoneAsync("+94770001234");
        var client = Factory.CreateClient().WithBearer(auth.AccessToken);
        (await client.PutAsync("/api/v1/me/photo", DoctorFlows.File(DoctorFlows.Png, "me.png"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var photoKey = (await Fixture.ScalarAsync<string>($"SELECT photo_storage_key FROM users WHERE id = '{auth.User.Id}'"))!;
        var booked = await client.BookedAsync(doctor.DoctorId, await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(3)));
        await Fixture.ExecuteSqlAsync($"""
            UPDATE users SET address = '12 Galle Road', allergies = 'Penicillin', date_of_birth = '1990-01-01', email = 'gone@example.com',
                normalized_email = 'GONE@EXAMPLE.COM' WHERE id = '{auth.User.Id}';
            INSERT INTO user_logins (login_provider, provider_key, provider_display_name, user_id) VALUES ('Google', 'sub-123', 'Google', '{auth.User.Id}');
            """);
        (await client.DeleteAsync("/api/v1/me", Ct)).EnsureSuccessStatusCode();

        Factory.Time.Advance(PlatformPolicy.ErasureGracePeriod - TimeSpan.FromHours(1));
        await Factory.RunJobAsync<UserErasureJob>();
        (await Fixture.ScalarAsync<DateTimeOffset?>($"SELECT anonymized_at FROM users WHERE id = '{auth.User.Id}'")).ShouldBeNull();

        Factory.Time.Advance(TimeSpan.FromHours(2));
        await Factory.RunJobAsync<UserErasureJob>();

        var row = await Fixture.ScalarAsync<string>($"""
            SELECT concat_ws('|', full_name, coalesce(email, '-'), coalesce(normalized_email, '-'), coalesce(phone_number, '-'), coalesce(address, '-'),
                coalesce(allergies, '-'), coalesce(date_of_birth::text, '-'), coalesce(photo_storage_key, '-'), coalesce(password_hash, '-'),
                status, (anonymized_at IS NOT NULL)::text)
            FROM users WHERE id = '{auth.User.Id}'
            """);
        row.ShouldBe($"{UserErasureJob.ErasedName}|-|-|-|-|-|-|-|-|deleted|true");
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM user_logins WHERE user_id = '{auth.User.Id}'")).ShouldBe(0);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM refresh_tokens WHERE user_id = '{auth.User.Id}' AND revoked_at IS NULL")).ShouldBe(0);
        File.Exists(Path.Combine(Factory.StorageRoot, photoKey)).ShouldBeFalse();
        (await Fixture.ScalarAsync<string>($"SELECT visit_patient_name FROM appointments WHERE id = '{booked.Id}'")).ShouldBe("Kamal Silva");

        var stampBefore = await Fixture.ScalarAsync<string>($"SELECT security_stamp FROM users WHERE id = '{auth.User.Id}'");
        await Factory.RunJobAsync<UserErasureJob>();
        (await Fixture.ScalarAsync<string>($"SELECT security_stamp FROM users WHERE id = '{auth.User.Id}'")).ShouldBe(stampBefore);
    }
}
