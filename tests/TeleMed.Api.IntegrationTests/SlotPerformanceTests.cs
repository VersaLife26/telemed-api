using System.Diagnostics;
using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Scheduling;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;

namespace TeleMed.Api.IntegrationTests;

public class SlotPerformanceTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private const int Runs = 30;

    // Target is under 50 ms of server time; the assertion is loose so a slow CI host does not flake, and the numbers are logged.
    [Fact]
    public async Task Slots_for_one_doctor_over_31_days_stay_fast()
    {
        var doctor = await Factory.BookableDoctorAsync();
        (await doctor.Client.PutJsonAsync("/api/v1/doctors/me/schedule", new
        {
            slotDurationMinutes = 15,
            bufferMinutes = 0,
            maxPerDay = 96,
            advanceDays = 30,
            timezone = "Asia/Colombo",
            workingHours = Enumerable.Range(0, 7).Select(d => new { dayOfWeek = d, startMinute = 0, endMinute = 1440 }).ToArray(),
        })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var patient = await Factory.PatientAsync();
        var today = Factory.Today();
        for (var day = 1; day <= 25; day++)
        {
            await patient.Client.BookedAsync(doctor.DoctorId, LocalTime(today.AddDays(day), 10));
        }

        var client = Factory.CreateClient();
        var url = $"/api/v1/doctors/{doctor.DoctorId}/slots?from={today:yyyy-MM-dd}&to={today.AddDays(30):yyyy-MM-dd}";
        var slots = 0;
        for (var i = 0; i < 5; i++)
        {
            slots = (await (await client.GetAsync(url, TestContext.Current.CancellationToken)).ReadAsync<SlotsDto>()).Slots.Count;
        }

        var samples = new List<double>();
        for (var i = 0; i < Runs; i++)
        {
            var watch = Stopwatch.StartNew();
            using var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
            await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        var median = samples[Runs / 2];
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"slots={slots} median={median:F1}ms p90={samples[(int)(Runs * 0.9)]:F1}ms max={samples[^1]:F1}ms");
        slots.ShouldBeGreaterThan(2_500);
        median.ShouldBeLessThan(250);
    }
}
