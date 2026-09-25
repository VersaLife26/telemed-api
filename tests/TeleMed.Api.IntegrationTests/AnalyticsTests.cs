using System.Net;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Analytics;
using TeleMed.Domain.Enums;
using static TeleMed.Api.IntegrationTests.Infrastructure.FinanceFlows;

namespace TeleMed.Api.IntegrationTests;

public class AnalyticsTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Seed(BookingFlows.BookableDoctor Doctor, DateOnly Today, DateOnly VisitDay);

    // Four real bookings on one day (completed, no-show, cancelled with a full refund, still confirmed), plus an unpaid
    // booking that expired and a completed test appointment, neither of which may count.
    private async Task<Seed> SeedAsync()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var today = Factory.Today();
        var completed = await Factory.CapturedAsync(patient, doctor.DoctorId, 9);
        var noShow = await Factory.CapturedAsync(patient, doctor.DoctorId, 10);
        await Factory.CapturedAsync(patient, doctor.DoctorId, 11);
        var cancelled = await Factory.CapturedAsync(patient, doctor.DoctorId, 12);
        var test = await Factory.CapturedAsync(patient, doctor.DoctorId, 13);
        var unpaid = await patient.Client.BookedAsync(doctor.DoctorId, BookingFlows.LocalTime(today.AddDays(8), 14));

        (await patient.Client.PostAsync($"/api/v1/appointments/{cancelled.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Factory.SettleAsync();
        await Fixture.MarkTestAsync(test.Id);
        await Fixture.ExecuteSqlAsync($"""
            UPDATE appointments SET status = 'completed', completed_at = now() WHERE id IN ('{completed.Id}', '{test.Id}');
            UPDATE appointments SET status = 'no_show', no_show_at = now() WHERE id = '{noShow.Id}';
            UPDATE appointments SET status = 'cancelled', cancelled_at = now(), cancelled_by = 'system' WHERE id = '{unpaid.Id}';
            """);
        return new Seed(doctor, today, today.AddDays(8));
    }

    [Fact]
    public async Task Admin_analytics_count_real_bookings_and_net_revenue_and_leave_out_test_and_unpaid_ones()
    {
        var seed = await SeedAsync();
        var admin = await Factory.AdminClientAsync(AdminRole.Support);
        var range = $"from={seed.Today:yyyy-MM-dd}&to={seed.VisitDay:yyyy-MM-dd}";

        var dashboard = await (await admin.GetAsync($"/api/v1/admin/analytics/dashboard?{range}", Ct)).ReadAsync<DashboardDto>();
        dashboard.Bookings.ShouldBe(4);
        dashboard.Completed.ShouldBe(1);
        dashboard.NoShows.ShouldBe(1);
        dashboard.Cancelled.ShouldBe(1);
        dashboard.GrossRevenueCents.ShouldBe(3 * Fee);
        dashboard.CommissionCents.ShouldBe(3 * Commission);
        dashboard.ActiveDoctors.ShouldBe(1);
        dashboard.NewPatients.ShouldBe(1);

        var revenue = await (await admin.GetAsync($"/api/v1/admin/analytics/revenue?{range}&granularity=day", Ct)).ReadAsync<List<RevenuePointDto>>();
        var day = revenue.ShouldHaveSingleItem();
        day.Period.ShouldBe(seed.Today);
        day.Payments.ShouldBe(4);
        day.CapturedCents.ShouldBe(4 * Fee);
        day.RefundedCents.ShouldBe(Fee);
        day.NetCents.ShouldBe(3 * Fee);
        var month = await (await admin.GetAsync($"/api/v1/admin/analytics/revenue?{range}&granularity=month", Ct)).ReadAsync<List<RevenuePointDto>>();
        month.ShouldHaveSingleItem().Period.ShouldBe(new DateOnly(seed.Today.Year, seed.Today.Month, 1));

        var bookings = await (await admin.GetAsync($"/api/v1/admin/analytics/bookings?{range}", Ct)).ReadAsync<List<BookingsDayDto>>();
        bookings.ShouldHaveSingleItem().ShouldBe(new BookingsDayDto(seed.VisitDay, 4, 1, 1, 1, 1));

        var top = await (await admin.GetAsync($"/api/v1/admin/analytics/top-doctors?{range}&limit=5", Ct)).ReadAsync<List<TopDoctorDto>>();
        var leader = top.ShouldHaveSingleItem();
        leader.DoctorId.ShouldBe(seed.Doctor.DoctorId);
        leader.Completed.ShouldBe(1);
        leader.NetCents.ShouldBe(3 * Fee);
        leader.CommissionCents.ShouldBe(3 * Commission);

        (await admin.GetAsync($"/api/v1/admin/analytics/top-doctors?limit=0", Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.GetAsync($"/api/v1/admin/analytics/dashboard?from={seed.VisitDay:yyyy-MM-dd}&to={seed.Today:yyyy-MM-dd}", Ct)).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Doctors_see_their_own_rates_revenue_and_peak_hours()
    {
        var seed = await SeedAsync();
        var client = await Factory.ClientForAsync(seed.Doctor.UserId);

        var visits = await (await client.GetAsync($"/api/v1/doctors/me/analytics?from={seed.VisitDay:yyyy-MM-dd}&to={seed.VisitDay:yyyy-MM-dd}", Ct))
            .ReadAsync<DoctorAnalyticsDto>();
        visits.Consultations.ShouldBe(4);
        visits.Completed.ShouldBe(1);
        visits.NoShows.ShouldBe(1);
        visits.Cancelled.ShouldBe(1);
        visits.CompletionRate.ShouldBe(0.25);
        visits.NoShowRate.ShouldBe(0.25);

        var money = await (await client.GetAsync($"/api/v1/doctors/me/analytics?from={seed.Today:yyyy-MM-dd}&to={seed.Today:yyyy-MM-dd}", Ct))
            .ReadAsync<DoctorAnalyticsDto>();
        money.GrossCents.ShouldBe(3 * Fee);
        money.NetCents.ShouldBe(3 * DoctorShare);

        var peaks = await (await client.GetAsync($"/api/v1/doctors/me/analytics/peak-hours?from={seed.VisitDay:yyyy-MM-dd}&to={seed.VisitDay:yyyy-MM-dd}", Ct))
            .ReadAsync<List<PeakHourDto>>();
        var dow = (int)seed.VisitDay.DayOfWeek;
        peaks.ShouldBe([new PeakHourDto(dow, 9, 1), new PeakHourDto(dow, 10, 1), new PeakHourDto(dow, 11, 1)]);

        var earnings = await (await client.GetAsync($"/api/v1/doctors/me/earnings?from={seed.Today:yyyy-MM-dd}&to={seed.Today:yyyy-MM-dd}", Ct))
            .ReadAsync<DoctorEarningsDto>();
        earnings.Payments.ShouldBe(4);
        earnings.GrossCents.ShouldBe(4 * Fee);
        earnings.RefundedCents.ShouldBe(Fee);
        (earnings.CommissionCents + earnings.ProviderFeeCents + earnings.NetCents).ShouldBe(earnings.GrossCents - earnings.RefundedCents);
        earnings.PendingPayoutCents.ShouldBe(3 * DoctorShare);
        earnings.PaidCents.ShouldBe(0);
    }
}
