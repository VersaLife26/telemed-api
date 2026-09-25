using TeleMed.Application.Appointments;
using TeleMed.Domain.Rules;

namespace TeleMed.Api.IntegrationTests.Infrastructure;

public static class FinanceFlows
{
    public const long Fee = 250_000;
    public const long Commission = 50_000;
    public const long ProviderFee = 7_500;
    public const long DoctorShare = Fee - Commission - ProviderFee;

    // More than MaxCardHoldLead ahead, so the mock payment is captured at once instead of held.
    public static async Task<AppointmentDto> CapturedAsync(this TeleMedApiFactory factory, BookingFlows.Patient patient, Guid doctorId, int hour)
    {
        var client = await factory.ClientForAsync(patient.UserId);
        var booked = await client.BookedAsync(doctorId, BookingFlows.LocalTime(factory.Today().AddDays(8), hour));
        await client.PayWithMockAsync(booked.Id);
        return booked;
    }

    // Payouts wait for the visit's outcome, so this captures a payment and completes its appointment at the current fake time.
    public static async Task<AppointmentDto> CompletedAsync(this ApiFixture fixture, BookingFlows.Patient patient, Guid doctorId, int hour)
    {
        var appointment = await fixture.Factory.CapturedAsync(patient, doctorId, hour);
        await fixture.MarkCompletedAsync(appointment.Id);
        return appointment;
    }

    public static Task MarkCompletedAsync(this ApiFixture fixture, Guid appointmentId) =>
        fixture.ExecuteSqlAsync($"UPDATE appointments SET status = 'completed', completed_at = '{fixture.Factory.Time.GetUtcNow():O}' WHERE id = '{appointmentId}'");

    public static Task MarkTestAsync(this ApiFixture fixture, Guid appointmentId) =>
        fixture.ExecuteSqlAsync($"UPDATE appointments SET is_test = true WHERE id = '{appointmentId}'");

    public static Task<Guid> PaymentIdAsync(this ApiFixture fixture, Guid appointmentId) =>
        fixture.ScalarAsync<Guid>($"SELECT id FROM payments WHERE appointment_id = '{appointmentId}'");

    // Moves the fake clock to the first Asia/Colombo wall-clock `hour` at or after `notBefore`.
    public static DateTimeOffset LocalHourAtOrAfter(DateTimeOffset notBefore, int hour)
    {
        var local = TimeZoneInfo.ConvertTime(notBefore, BookingFlows.Colombo).DateTime;
        var target = local.Date.AddHours(hour);
        if (target < local)
        {
            target = target.AddDays(1);
        }

        return new DateTimeOffset(target, BookingFlows.Colombo.GetUtcOffset(target)).ToUniversalTime();
    }

    public static void AdvanceTo(this TeleMedApiFactory factory, DateTimeOffset instant)
    {
        var delta = instant - factory.Time.GetUtcNow();
        if (delta > TimeSpan.Zero)
        {
            factory.Time.Advance(delta);
        }
    }

    public static DateOnly LocalDate(DateTimeOffset instant) => SlotPlanner.LocalDate(instant, BookingFlows.Colombo);
}
