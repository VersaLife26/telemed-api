using System.Net;
using System.Text.Json;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Scheduling;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;
using static TeleMed.Api.IntegrationTests.Infrastructure.DoctorFlows;

namespace TeleMed.Api.IntegrationTests;

public class SchedulingTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static readonly TimeZoneInfo Colombo = IanaTimeZone.Find("Asia/Colombo");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateOnly Tomorrow => SlotPlanner.LocalDate(Factory.Time.GetUtcNow(), Colombo).AddDays(1);

    private static object Schedule(int startHour, int endHour, int duration = 30, int buffer = 0, string timezone = "Asia/Colombo") => new
    {
        slotDurationMinutes = duration,
        bufferMinutes = buffer,
        maxPerDay = 50,
        advanceDays = 30,
        timezone,
        workingHours = Enumerable.Range(0, 7).Select(d => new { dayOfWeek = d, startMinute = startHour * 60, endMinute = endHour * 60 }).ToArray(),
    };

    private static DateTimeOffset At(DateOnly date, int hour, int minute)
    {
        var local = date.ToDateTime(new TimeOnly(hour, minute));
        return new DateTimeOffset(local, Colombo.GetUtcOffset(local)).ToUniversalTime();
    }

    private async Task<SlotsDto> SlotsAsync(Guid doctorId, DateOnly from, DateOnly to) =>
        await (await Factory.CreateClient().GetAsync($"/api/v1/doctors/{doctorId}/slots?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", Ct))
            .ReadAsync<SlotsDto>();

    private static string[] Describe(SlotsDto slots) =>
        slots.Slots.Select(s => $"{TimeZoneInfo.ConvertTime(s.StartAt, Colombo):MM-dd HH:mm}{(s.Available ? "" : " x")}").ToArray();

    private static string[] Expected(IEnumerable<(DateOnly Date, string Time, bool Available)> slots) =>
        slots.Select(s => $"{s.Date:MM-dd} {s.Time}{(s.Available ? "" : " x")}").ToArray();

    [Fact]
    public async Task Every_calendar_write_shows_up_on_the_next_slots_read()
    {
        var (doctorId, doctor) = await Factory.ApprovedDoctorAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Ops);
        var (d1, d2, d3) = (Tomorrow, Tomorrow.AddDays(1), Tomorrow.AddDays(2));
        IEnumerable<(DateOnly, string, bool)> Day(DateOnly date, string first, string second, bool firstAvailable = true) =>
            [(date, first, firstAvailable), (date, second, true)];

        (await SlotsAsync(doctorId, d1, d3)).Slots.ShouldBeEmpty();

        (await doctor.PutJsonAsync("/api/v1/doctors/me/schedule", Schedule(9, 10))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var morning = await SlotsAsync(doctorId, d1, d3);
        morning.Timezone.ShouldBe("Asia/Colombo");
        Describe(morning).ShouldBe(Expected([.. Day(d1, "09:00", "09:30"), .. Day(d2, "09:00", "09:30"), .. Day(d3, "09:00", "09:30")]));

        (await doctor.PutJsonAsync("/api/v1/doctors/me/schedule", Schedule(14, 15))).StatusCode.ShouldBe(HttpStatusCode.OK);
        Describe(await SlotsAsync(doctorId, d1, d3))
            .ShouldBe(Expected([.. Day(d1, "14:00", "14:30"), .. Day(d2, "14:00", "14:30"), .. Day(d3, "14:00", "14:30")]));

        var holiday = await (await doctor.PostJsonAsync("/api/v1/doctors/me/holidays", new { date = d1, reason = "Conference" }))
            .ReadAsync<HolidayDto>(HttpStatusCode.Created);
        Describe(await SlotsAsync(doctorId, d1, d3)).ShouldBe(Expected([.. Day(d2, "14:00", "14:30"), .. Day(d3, "14:00", "14:30")]));

        var block = await (await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/slot-blocks",
                new { startAt = At(d2, 14, 10), endAt = At(d2, 14, 20), reason = "Clinic maintenance" }))
            .ReadAsync<SlotBlockDto>(HttpStatusCode.Created);
        Describe(await SlotsAsync(doctorId, d1, d3))
            .ShouldBe(Expected([.. Day(d2, "14:00", "14:30", firstAvailable: false), .. Day(d3, "14:00", "14:30")]));

        var platform = await (await admin.PostJsonAsync("/api/v1/admin/holidays", new { date = d3, reason = "Poya" }))
            .ReadAsync<HolidayDto>(HttpStatusCode.Created);
        Describe(await SlotsAsync(doctorId, d1, d3)).ShouldBe(Expected(Day(d2, "14:00", "14:30", firstAvailable: false)));

        (await admin.DeleteAsync($"/api/v1/admin/slot-blocks/{block.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await doctor.DeleteAsync($"/api/v1/doctors/me/holidays/{holiday.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/v1/admin/holidays/{platform.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Describe(await SlotsAsync(doctorId, d1, d3))
            .ShouldBe(Expected([.. Day(d1, "14:00", "14:30"), .. Day(d2, "14:00", "14:30"), .. Day(d3, "14:00", "14:30")]));
    }

    [Fact]
    public async Task Schedule_put_replaces_settings_and_hours_in_one_save()
    {
        var (doctorId, doctor) = await Factory.ApprovedDoctorAsync();

        var initial = await (await doctor.GetAsync("/api/v1/doctors/me/schedule", Ct)).ReadAsync<ScheduleDto>();
        (initial.SlotDurationMinutes, initial.BufferMinutes, initial.MaxPerDay, initial.AdvanceDays, initial.Timezone).ShouldBe((
            PlatformPolicy.DefaultSlotDurationMinutes,
            PlatformPolicy.DefaultBufferMinutes,
            PlatformPolicy.DefaultMaxPerDay,
            PlatformPolicy.DefaultAdvanceDays,
            PlatformPolicy.TimeZoneId));
        initial.WorkingHours.ShouldBeEmpty();

        var first = await (await doctor.PutJsonAsync("/api/v1/doctors/me/schedule", new
        {
            slotDurationMinutes = 20,
            bufferMinutes = 10,
            maxPerDay = 12,
            advanceDays = 60,
            timezone = "Asia/Dubai",
            workingHours = new[]
            {
                new { dayOfWeek = 1, startMinute = 14 * 60, endMinute = 17 * 60 },
                new { dayOfWeek = 1, startMinute = 9 * 60, endMinute = 12 * 60 },
                new { dayOfWeek = 0, startMinute = 20 * 60, endMinute = 1440 },
            },
        })).ReadAsync<ScheduleUpdatedDto>();
        first.AppointmentsOutsideNewHours.ShouldBe(0);
        first.Timezone.ShouldBe("Asia/Dubai");
        first.WorkingHours.ShouldBe([new(0, 1200, 1440), new(1, 540, 720), new(1, 840, 1020)]);

        await doctor.PutJsonAsync("/api/v1/doctors/me/schedule", new
        {
            slotDurationMinutes = 20,
            bufferMinutes = 10,
            maxPerDay = 12,
            advanceDays = 60,
            timezone = "Asia/Dubai",
            workingHours = new[] { new { dayOfWeek = 1, startMinute = 9 * 60, endMinute = 13 * 60 } },
        });
        var admin = await Factory.AdminClientAsync(AdminRole.Support);
        var seenByAdmin = await (await admin.GetAsync($"/api/v1/admin/doctors/{doctorId}/schedule", Ct)).ReadAsync<ScheduleDto>();
        seenByAdmin.WorkingHours.ShouldBe([new(1, 540, 780)]);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM working_hours WHERE doctor_id = '{doctorId}'")).ShouldBe(1);

        var cleared = await (await admin.PutJsonAsync($"/api/v1/admin/doctors/{doctorId}/schedule",
                new { slotDurationMinutes = 15, bufferMinutes = 0, maxPerDay = 5, advanceDays = 7, timezone = "Asia/Colombo", workingHours = Array.Empty<object>() }))
            .ReadAsync<ScheduleUpdatedDto>();
        cleared.WorkingHours.ShouldBeEmpty();
        (await (await doctor.GetAsync("/api/v1/doctors/me/schedule", Ct)).ReadAsync<ScheduleDto>()).MaxPerDay.ShouldBe(5);
    }

    public static TheoryData<string, object> InvalidSchedules => new()
    {
        { "workingHours", new { slotDurationMinutes = 30, bufferMinutes = 0, maxPerDay = 10, advanceDays = 30, timezone = "Asia/Colombo",
            workingHours = new[] { new { dayOfWeek = 2, startMinute = 540, endMinute = 720 }, new { dayOfWeek = 2, startMinute = 700, endMinute = 800 } } } },
        { "timezone", new { slotDurationMinutes = 30, bufferMinutes = 0, maxPerDay = 10, advanceDays = 30, timezone = "Mars/Olympus", workingHours = Array.Empty<object>() } },
        { "slotDurationMinutes", new { slotDurationMinutes = 4, bufferMinutes = 0, maxPerDay = 10, advanceDays = 30, timezone = "Asia/Colombo", workingHours = Array.Empty<object>() } },
        { "bufferMinutes", new { slotDurationMinutes = 30, bufferMinutes = 121, maxPerDay = 10, advanceDays = 30, timezone = "Asia/Colombo", workingHours = Array.Empty<object>() } },
        { "maxPerDay", new { slotDurationMinutes = 30, bufferMinutes = 0, maxPerDay = 0, advanceDays = 30, timezone = "Asia/Colombo", workingHours = Array.Empty<object>() } },
        { "advanceDays", new { slotDurationMinutes = 30, bufferMinutes = 0, maxPerDay = 10, advanceDays = 181, timezone = "Asia/Colombo", workingHours = Array.Empty<object>() } },
        { "workingHours[0].endMinute", new { slotDurationMinutes = 30, bufferMinutes = 0, maxPerDay = 10, advanceDays = 30, timezone = "Asia/Colombo",
            workingHours = new[] { new { dayOfWeek = 2, startMinute = 540, endMinute = 540 } } } },
        { "workingHours[0].dayOfWeek", new { slotDurationMinutes = 30, bufferMinutes = 0, maxPerDay = 10, advanceDays = 30, timezone = "Asia/Colombo",
            workingHours = new[] { new { dayOfWeek = 7, startMinute = 540, endMinute = 600 } } } },
    };

    [Theory]
    [MemberData(nameof(InvalidSchedules))]
    public async Task Invalid_schedules_are_rejected(string field, object body)
    {
        var (_, doctor) = await Factory.ApprovedDoctorAsync();

        var response = await doctor.PutJsonAsync("/api/v1/doctors/me/schedule", body);

        var content = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, content);
        JsonDocument.Parse(content).RootElement.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue(content);
        (await (await doctor.GetAsync("/api/v1/doctors/me/schedule", Ct)).ReadAsync<ScheduleDto>()).WorkingHours.ShouldBeEmpty();
    }

    [Fact]
    public async Task Doctors_manage_only_their_own_holidays()
    {
        var (_, doctor) = await Factory.ApprovedDoctorAsync();
        var (_, other) = await Factory.ApprovedDoctorAsync(new ApplicationSpec { Phone = "+94771000002", Email = "other@example.com", SlmcNumber = "22222" });
        var admin = await Factory.AdminClientAsync(AdminRole.Admin);
        var date = Tomorrow.AddDays(5);

        var own = await (await doctor.PostJsonAsync("/api/v1/doctors/me/holidays", new { date, reason = "Leave" })).ReadAsync<HolidayDto>(HttpStatusCode.Created);
        var duplicate = await doctor.PostJsonAsync("/api/v1/doctors/me/holidays", new { date, reason = "Again" });
        var platform = await (await admin.PostJsonAsync("/api/v1/admin/holidays", new { date = date.AddDays(1), reason = "Poya" }))
            .ReadAsync<HolidayDto>(HttpStatusCode.Created);
        var platformDuplicate = await admin.PostJsonAsync("/api/v1/admin/holidays", new { date = date.AddDays(1), reason = "Poya" });

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.ProblemCodeAsync()).ShouldBe("holiday_exists");
        platformDuplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await platformDuplicate.ProblemCodeAsync()).ShouldBe("holiday_exists");

        var listed = await (await doctor.GetAsync("/api/v1/doctors/me/holidays", Ct)).ReadAsync<List<HolidayDto>>();
        listed.Select(h => h.Id).ShouldBe([own.Id, platform.Id]);
        platform.DoctorId.ShouldBeNull();
        (await (await other.GetAsync("/api/v1/doctors/me/holidays", Ct)).ReadAsync<List<HolidayDto>>()).Select(h => h.Id).ShouldBe([platform.Id]);
        (await (await doctor.GetAsync($"/api/v1/doctors/me/holidays?from={date.AddDays(1):yyyy-MM-dd}", Ct)).ReadAsync<List<HolidayDto>>())
            .Select(h => h.Id).ShouldBe([platform.Id]);
        (await (await admin.GetAsync("/api/v1/admin/holidays", Ct)).ReadAsync<List<HolidayDto>>()).Select(h => h.Id).ShouldBe([platform.Id]);

        (await other.DeleteAsync($"/api/v1/doctors/me/holidays/{own.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await doctor.DeleteAsync($"/api/v1/doctors/me/holidays/{platform.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await doctor.DeleteAsync($"/api/v1/doctors/me/holidays/{own.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM holidays")).ShouldBe(1);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM audit_logs WHERE entity_type = 'holidays' AND action = 'deleted'")).ShouldBe(1);
    }

    [Fact]
    public async Task Slot_range_defaults_to_two_weeks_and_is_capped()
    {
        var (doctorId, doctor) = await Factory.ApprovedDoctorAsync();
        await doctor.PutJsonAsync("/api/v1/doctors/me/schedule", Schedule(23, 24));
        var today = Tomorrow.AddDays(-1);
        var client = Factory.CreateClient();

        var defaults = await (await client.GetAsync($"/api/v1/doctors/{doctorId}/slots", Ct)).ReadAsync<SlotsDto>();
        var dates = defaults.Slots.Select(s => SlotPlanner.LocalDate(s.StartAt, Colombo)).Distinct().ToList();
        dates[^1].ShouldBe(today.AddDays(13));
        dates.ShouldAllBe(d => d >= today);

        (await client.GetAsync($"/api/v1/doctors/{doctorId}/slots?from={today:yyyy-MM-dd}&to={today.AddDays(31):yyyy-MM-dd}", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync($"/api/v1/doctors/{doctorId}/slots?from={today:yyyy-MM-dd}&to={today.AddDays(-1):yyyy-MM-dd}", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await SlotsAsync(doctorId, today, today.AddDays(30))).Slots.Count.ShouldBe(2 * 30);
        (await SlotsAsync(doctorId, today.AddDays(40), today.AddDays(41))).Slots.ShouldBeEmpty();
        (await client.GetAsync($"/api/v1/doctors/{Guid.NewGuid()}/slots", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Suspended_doctors_have_no_slots_and_cannot_edit_but_admins_can()
    {
        var (doctorId, doctor) = await Factory.ApprovedDoctorAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Support);
        await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/suspend", new { reason = "Review" });

        (await Factory.CreateClient().GetAsync($"/api/v1/doctors/{doctorId}/slots", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var write = await doctor.PutJsonAsync("/api/v1/doctors/me/schedule", Schedule(9, 10));
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await write.ProblemCodeAsync()).ShouldBe("doctor_suspended");
        (await doctor.PostJsonAsync("/api/v1/doctors/me/holidays", new { date = Tomorrow, reason = "Leave" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await doctor.GetAsync("/api/v1/doctors/me/schedule", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await admin.PutJsonAsync($"/api/v1/admin/doctors/{doctorId}/schedule", Schedule(9, 10))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/holidays", new { date = Tomorrow, reason = "Leave" })).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Patients_cannot_touch_schedules()
    {
        var (doctorId, _) = await Factory.ApprovedDoctorAsync();
        var patient = Factory.CreateClient().WithBearer((await Factory.CreateUserAsync(UserRole.Patient)).AccessToken);
        patient.DefaultRequestHeaders.Add("Origin", TeleMedApiFactory.AdminOrigin);

        (await patient.PutJsonAsync($"/api/v1/admin/doctors/{doctorId}/schedule", Schedule(9, 10))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await patient.PostJsonAsync("/api/v1/admin/holidays", new { date = Tomorrow, reason = "Poya" })).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await patient.PutJsonAsync("/api/v1/doctors/me/schedule", Schedule(9, 10))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await patient.GetAsync("/api/v1/doctors/me/holidays", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM working_hours")).ShouldBe(0);
    }

    [Fact]
    public async Task Slot_blocks_are_listed_by_overlap_and_validated()
    {
        var (doctorId, _) = await Factory.ApprovedDoctorAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Ops);
        var start = At(Tomorrow, 9, 0);

        (await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/slot-blocks", new { startAt = start, endAt = start, reason = "x" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync($"/api/v1/admin/doctors/{Guid.NewGuid()}/slot-blocks", new { startAt = start, endAt = start.AddHours(1), reason = "x" }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var early = await (await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/slot-blocks", new { startAt = start, endAt = start.AddHours(1), reason = "A" }))
            .ReadAsync<SlotBlockDto>(HttpStatusCode.Created);
        var late = await (await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/slot-blocks",
                new { startAt = start.AddHours(3), endAt = start.AddHours(4), reason = "B" }))
            .ReadAsync<SlotBlockDto>(HttpStatusCode.Created);

        var all = await (await admin.GetAsync($"/api/v1/admin/doctors/{doctorId}/slot-blocks", Ct)).ReadAsync<List<SlotBlockDto>>();
        var from = Uri.EscapeDataString(start.AddMinutes(30).ToString("O"));
        var to = Uri.EscapeDataString(start.AddHours(2).ToString("O"));
        var overlapping = await (await admin.GetAsync($"/api/v1/admin/doctors/{doctorId}/slot-blocks?from={from}&to={to}", Ct))
            .ReadAsync<List<SlotBlockDto>>();

        all.Select(b => b.Id).ShouldBe([early.Id, late.Id]);
        overlapping.Select(b => b.Id).ShouldBe([early.Id]);
        early.StartAt.ShouldBe(start);
        (await admin.DeleteAsync($"/api/v1/admin/slot-blocks/{Guid.NewGuid()}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Holidays_and_slot_blocks_in_the_past_are_rejected()
    {
        var (doctorId, doctor) = await Factory.ApprovedDoctorAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Ops);
        var yesterday = Tomorrow.AddDays(-2);
        var now = Factory.Time.GetUtcNow();

        (await doctor.PostJsonAsync("/api/v1/doctors/me/holidays", new { date = yesterday, reason = "Late" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync("/api/v1/admin/holidays", new { date = yesterday, reason = "Late" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/slot-blocks", new { startAt = now.AddHours(-2), endAt = now.AddHours(-1), reason = "x" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await doctor.PostJsonAsync("/api/v1/doctors/me/holidays", new { date = Tomorrow.AddDays(-1), reason = "Today" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await admin.PostJsonAsync($"/api/v1/admin/doctors/{doctorId}/slot-blocks", new { startAt = now.AddHours(-1), endAt = now.AddHours(1), reason = "Ongoing" }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
