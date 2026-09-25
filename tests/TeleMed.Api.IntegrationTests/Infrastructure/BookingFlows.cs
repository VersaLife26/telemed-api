using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Auth;
using TeleMed.Application.Jobs;
using TeleMed.Application.Payments;
using TeleMed.Application.Reschedules;
using TeleMed.Application.Scheduling;
using TeleMed.Application.Users;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;
using TeleMed.Infrastructure.Payments;

namespace TeleMed.Api.IntegrationTests.Infrastructure;

public static class BookingFlows
{
    public static readonly TimeZoneInfo Colombo = IanaTimeZone.Find("Asia/Colombo");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed record BookableDoctor(Guid DoctorId, Guid UserId, HttpClient Client, DoctorFlows.ApplicationSpec Spec);

    public sealed record Patient(Guid UserId, HttpClient Client);

    public static async Task<BookableDoctor> BookableDoctorAsync(this TeleMedApiFactory factory, DoctorFlows.ApplicationSpec? spec = null)
    {
        spec ??= new DoctorFlows.ApplicationSpec();
        var (doctorId, client) = await factory.ApprovedDoctorAsync(spec);
        (await client.PutJsonAsync("/api/v1/doctors/me/schedule", new
        {
            slotDurationMinutes = 30,
            bufferMinutes = 0,
            maxPerDay = 48,
            advanceDays = 30,
            timezone = "Asia/Colombo",
            workingHours = Enumerable.Range(0, 7).Select(d => new { dayOfWeek = d, startMinute = 0, endMinute = 1440 }).ToArray(),
        })).StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await (await client.GetAsync("/api/v1/me", Ct)).ReadAsync<MeDto>();
        return new BookableDoctor(doctorId, me.Id, client, spec);
    }

    public static DoctorFlows.ApplicationSpec SecondDoctor => new() { Phone = "+94771000002", Email = "second@example.com", SlmcNumber = "22222" };

    public static async Task<Patient> PatientAsync(this TeleMedApiFactory factory)
    {
        var auth = await factory.CreateUserAsync(UserRole.Patient);
        return new Patient(auth.User.Id, factory.CreateClient().WithBearer(auth.AccessToken));
    }

    // Access tokens live 15 minutes, so tests that move the clock re-issue them.
    public static async Task<HttpClient> ClientForAsync(this TeleMedApiFactory factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = (await scope.ServiceProvider.GetRequiredService<IUserAccounts>().FindByIdAsync(userId, Ct))!;
        var session = scope.ServiceProvider.GetRequiredService<SessionIssuer>().Issue(user);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return factory.CreateClient().WithBearer(session.Response.AccessToken);
    }

    public static async Task<SlotsDto> SlotsAsync(this TeleMedApiFactory factory, Guid doctorId, int days = 10)
    {
        var today = SlotPlanner.LocalDate(factory.Time.GetUtcNow(), Colombo);
        return await (await factory.CreateClient().GetAsync($"/api/v1/doctors/{doctorId}/slots?from={today:yyyy-MM-dd}&to={today.AddDays(days - 1):yyyy-MM-dd}", Ct))
            .ReadAsync<SlotsDto>();
    }

    public static async Task<DateTimeOffset> FreeSlotAsync(this TeleMedApiFactory factory, Guid doctorId, TimeSpan after)
    {
        var earliest = factory.Time.GetUtcNow() + after;
        var today = SlotPlanner.LocalDate(earliest, Colombo);
        var slots = await (await factory.CreateClient().GetAsync($"/api/v1/doctors/{doctorId}/slots?from={today:yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}", Ct))
            .ReadAsync<SlotsDto>();
        return slots.Slots.First(s => s.Available && s.StartAt >= earliest).StartAt;
    }

    public static object BookingBody(Guid doctorId, DateTimeOffset startAt, decimal? weightKg = 70.5m, string? visitRelation = null) => new
    {
        doctorId,
        startAt,
        visitPatient = new { name = "Kamal Silva", dateOfBirth = "1990-04-01", sex = "male", weightKg, allergies = "Penicillin" },
        intake = new { symptoms = "Fever for two days", visitRelation },
    };

    public static Task<HttpResponseMessage> BookAsync(this HttpClient patient, Guid doctorId, DateTimeOffset startAt) =>
        patient.PostJsonAsync("/api/v1/appointments", BookingBody(doctorId, startAt));

    public static async Task<AppointmentDto> BookedAsync(this HttpClient patient, Guid doctorId, DateTimeOffset startAt) =>
        await (await patient.BookAsync(doctorId, startAt)).ReadAsync<AppointmentDto>(HttpStatusCode.Created);

    public static async Task<PaymentIntentDto> IntentAsync(this HttpClient patient, Guid appointmentId, string provider = "mock") =>
        await (await patient.PostJsonAsync($"/api/v1/appointments/{appointmentId}/payment/intent", new { provider })).ReadAsync<PaymentIntentDto>();

    public static async Task<PaymentDto> PayWithMockAsync(this HttpClient patient, Guid appointmentId, string outcome = "succeed")
    {
        var intent = await patient.IntentAsync(appointmentId);
        return await (await patient.PostJsonAsync($"/api/v1/payments/{intent.PaymentId}/mock/complete", new { outcome })).ReadAsync<PaymentDto>();
    }

    public static async Task<AppointmentDto> AppointmentAsync(this HttpClient client, Guid id) =>
        await (await client.GetAsync($"/api/v1/appointments/{id}", Ct)).ReadAsync<AppointmentDto>();

    public static Task<HttpResponseMessage> PayHereNotifyAsync(
        this TeleMedApiFactory factory, Guid paymentId, long amountCents, string statusCode = "2", string? signature = null, string payHereId = "320027000123")
    {
        var orderId = paymentId.ToString("D");
        var amount = PayHereSignature.FormatAmount(amountCents);
        var fields = new Dictionary<string, string>
        {
            ["merchant_id"] = TeleMedApiFactory.PayHereMerchantId,
            ["order_id"] = orderId,
            ["payment_id"] = payHereId,
            ["payhere_amount"] = amount,
            ["payhere_currency"] = "LKR",
            ["status_code"] = statusCode,
            ["md5sig"] = signature ?? PayHereSignature.NotifyHash(
                TeleMedApiFactory.PayHereMerchantId, orderId, amount, "LKR", statusCode, PayHereSignature.Md5Upper(TeleMedApiFactory.PayHereMerchantSecret)),
        };
        return factory.CreateClient().PostAsync("/api/v1/webhooks/payhere", new FormUrlEncodedContent(fields), Ct);
    }

    public static async Task RunJobAsync<TJob>(this TeleMedApiFactory factory)
        where TJob : IBackgroundJob
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TJob>().RunAsync(Ct);
    }

    public static Task ExpireUnpaidAsync(this TeleMedApiFactory factory) => factory.RunJobAsync<ExpireUnpaidBookingsJob>();

    public static Task SettleAsync(this TeleMedApiFactory factory) => factory.RunJobAsync<PaymentSettlementJob>();

    public static Task ExpireReschedulesAsync(this TeleMedApiFactory factory) => factory.RunJobAsync<ExpireRescheduleRequestsJob>();

    public static DateTimeOffset LocalTime(DateOnly date, int hour, int minute = 0)
    {
        var local = date.ToDateTime(new TimeOnly(hour, minute));
        return new DateTimeOffset(local, Colombo.GetUtcOffset(local)).ToUniversalTime();
    }

    public static DateOnly Today(this TeleMedApiFactory factory) => SlotPlanner.LocalDate(factory.Time.GetUtcNow(), Colombo);

    public static async Task<AppointmentDto> PaidAsync(this Patient patient, Guid doctorId, DateTimeOffset startAt)
    {
        var booked = await patient.Client.BookedAsync(doctorId, startAt);
        await patient.Client.PayWithMockAsync(booked.Id);
        return booked;
    }

    public static Task<HttpResponseMessage> ProposeAsync(this HttpClient doctor, Guid appointmentId, DateTimeOffset proposedStartAt) =>
        doctor.PostJsonAsync($"/api/v1/appointments/{appointmentId}/reschedule-requests", new { proposedStartAt, reason = "Clinic emergency" });

    public static async Task<RescheduleRequestDto> ProposedAsync(this HttpClient doctor, Guid appointmentId, DateTimeOffset proposedStartAt) =>
        await (await doctor.ProposeAsync(appointmentId, proposedStartAt)).ReadAsync<RescheduleRequestDto>(HttpStatusCode.Created);

    public static async Task<bool> IsAvailableAsync(this TeleMedApiFactory factory, Guid doctorId, DateTimeOffset startAt) =>
        (await factory.SlotsAsync(doctorId, 12)).Slots.Any(s => s.StartAt == startAt && s.Available);
}
