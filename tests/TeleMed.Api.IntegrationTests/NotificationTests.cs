using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Jobs;
using TeleMed.Application.Notifications;
using TeleMed.Application.Notifications.Templates;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;

namespace TeleMed.Api.IntegrationTests;

public class NotificationTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset SampleTime = new(2026, 10, 1, 4, 30, 0, TimeSpan.Zero);

    private static readonly INotificationModel[] SampleModels =
    [
        new BookingConfirmedModel("Nimal Perera", SampleTime, 250_000, "LKR"),
        new AppointmentCancelledModel("Nimal Perera", SampleTime),
        new AppointmentCancelledForDoctorModel(SampleTime),
        ReminderModel.DayBefore("Nimal Perera", SampleTime),
        ReminderModel.HourBefore("Nimal Perera", SampleTime),
        new PaymentFailedModel(250_000, "LKR"),
        new PrescriptionReadyModel("Nimal Perera", "https://app.telemed.test/prescriptions/1"),
        DoctorApplicationModel.Submitted("Nimal Perera"),
        DoctorApplicationModel.Approved("Nimal Perera"),
        DoctorApplicationModel.Rejected("Nimal Perera", "SLMC number could not be verified"),
        new RescheduleRequestedModel("Nimal Perera", SampleTime, SampleTime.AddDays(1)),
        new RescheduleConfirmedModel("Nimal Perera", SampleTime),
        new RescheduleAcceptedForDoctorModel(SampleTime),
        new DoctorRunningLateModel("Nimal Perera"),
        new EarlyJoinOfferedModel("Nimal Perera", "https://app.telemed.test/join/1"),
        new PaymentRefundedModel(250_000, "LKR"),
    ];

    [Fact]
    public void Every_template_renders_in_every_locale()
    {
        SampleModels.Select(m => m.TemplateKey).Order().ShouldBe(NotificationTemplates.All.Keys.Order());
        foreach (var model in SampleModels)
        {
            var template = NotificationTemplates.All[model.TemplateKey];
            (template.Email ?? (object?)template.Sms).ShouldNotBeNull(model.TemplateKey);
            foreach (var locale in Enum.GetValues<Language>())
            {
                var context = $"{model.TemplateKey}/{locale}";
                if (template.Email is not null)
                {
                    template.Email.Keys.ShouldContain(locale, context);
                    var email = template.RenderEmail(locale, model.Values())!;
                    email.Subject.ShouldNotBeNullOrWhiteSpace(context);
                    email.Body.ShouldNotBeNullOrWhiteSpace(context);
                    (email.Subject + email.Body).ShouldNotContain("{", customMessage: context);
                }

                if (template.Sms is not null)
                {
                    template.Sms.Keys.ShouldContain(locale, context);
                    var sms = template.RenderSms(locale, model.Values())!;
                    sms.ShouldNotBeNullOrWhiteSpace(context);
                    sms.ShouldNotContain("{", customMessage: context);
                }
            }
        }

        var confirmed = NotificationTemplates.All[TemplateKeys.BookingConfirmed].RenderSms(Language.En, SampleModels[0].Values());
        confirmed.ShouldBe("Your appointment with Dr. Nimal Perera is confirmed for 01 Oct 2026, 10:00. Fee: Rs. 2,500.00. Thank you for booking.");
    }

    [Fact]
    public async Task Paying_enqueues_the_confirmation_and_only_the_dispatcher_sends_it()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var booked = await patient.PaidAsync(doctor.DoctorId, LocalTime(Factory.Today().AddDays(2), 10));

        var row = $"SELECT concat_ws('|', channel, status, recipient, dedupe_key) FROM notifications WHERE template_key = 'booking_confirmed'";
        var email = await Fixture.ScalarAsync<string>($"SELECT email FROM users WHERE id = '{patient.UserId}'");
        (await Fixture.ScalarAsync<string>(row)).ShouldBe($"email|pending|{email}|appt:{booked.Id}:confirmed:email");
        (await CapturedAsync(email!)).ShouldBe(0);

        await Factory.RunJobAsync<NotificationDispatcherJob>();

        (await Fixture.ScalarAsync<string>(row)).ShouldBe($"email|sent|{email}|appt:{booked.Id}:confirmed:email");
        (await Fixture.ScalarAsync<string>($"SELECT subject FROM captured_messages WHERE recipient = '{email}'"))
            .ShouldBe("Your appointment with Dr. Nimal Perera is confirmed");
        (await Fixture.ScalarAsync<string>($"SELECT body FROM captured_messages WHERE recipient = '{email}'"))!.ShouldContain("Fee: Rs. 2,500.00");

        await Factory.RunJobAsync<NotificationDispatcherJob>();
        (await CapturedAsync(email!)).ShouldBe(1);
    }

    [Fact]
    public async Task Cancellations_tell_the_other_party_and_payment_failures_tell_the_patient()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var day = Factory.Today().AddDays(2);
        var byPatient = await patient.PaidAsync(doctor.DoctorId, LocalTime(day, 10));
        var byDoctor = await patient.PaidAsync(doctor.DoctorId, LocalTime(day, 11));
        var unpaid = await patient.Client.BookedAsync(doctor.DoctorId, LocalTime(day, 12));
        await patient.Client.PayWithMockAsync(unpaid.Id, "fail");

        (await patient.Client.PostAsync($"/api/v1/appointments/{byPatient.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await doctor.Client.PostAsync($"/api/v1/appointments/{byDoctor.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await patient.Client.PostAsync($"/api/v1/appointments/{unpaid.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await KeysAsync("appointment_cancelled_doctor")).ShouldBe([$"appt:{byPatient.Id}:cancelled:doctor:email", $"appt:{byPatient.Id}:cancelled:doctor:sms"]);
        (await KeysAsync("appointment_cancelled")).ShouldBe([$"appt:{byDoctor.Id}:cancelled:patient:email"]);
        (await Fixture.ScalarAsync<string>("SELECT recipient FROM notifications WHERE template_key = 'appointment_cancelled_doctor' AND channel = 'sms'"))
            .ShouldBe(doctor.Spec.Phone);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM notifications WHERE template_key = 'payment_failed'")).ShouldBe(1);
    }

    [Fact]
    public async Task Reschedule_proposals_and_acceptance_notify_patient_and_doctor()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var day = Factory.Today().AddDays(2);
        var booked = await patient.PaidAsync(doctor.DoctorId, LocalTime(day, 10));

        var request = await doctor.Client.ProposedAsync(booked.Id, LocalTime(day, 14));
        (await KeysAsync("reschedule_requested")).ShouldBe([$"reschedule:{request.Id}:requested:email"]);

        (await patient.Client.PostAsync($"/api/v1/reschedule-requests/{request.Id}/accept", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await KeysAsync("reschedule_confirmed")).ShouldBe([$"reschedule:{request.Id}:accepted:patient:email"]);
        (await KeysAsync("reschedule_accepted_doctor")).ShouldBe(
            [$"reschedule:{request.Id}:accepted:doctor:email", $"reschedule:{request.Id}:accepted:doctor:sms"]);
    }

    [Fact]
    public async Task Doctor_applications_email_the_applicant_and_reach_the_admin_inbox()
    {
        var submitted = await Factory.SubmitApplicationAsync();
        var rejected = await Factory.SubmitApplicationAsync(SecondDoctor);
        var admin = await Factory.AdminClientAsync(AdminRole.Support);

        await admin.ApproveAsync(submitted.Id);
        (await admin.PostJsonAsync($"/api/v1/admin/doctor-applications/{rejected.Id}/reject", new { reason = "SLMC number not found" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        await Factory.RunJobAsync<NotificationDispatcherJob>();

        (await KeysAsync("doctor_application_submitted")).ShouldBe(
            [$"application:{submitted.Id}:submitted:email", $"application:{rejected.Id}:submitted:email"], ignoreOrder: true);
        (await KeysAsync("doctor_application_approved")).ShouldBe([$"application:{submitted.Id}:approved:email"]);
        (await KeysAsync("doctor_application_rejected")).ShouldBe([$"application:{rejected.Id}:rejected:email"]);
        (await Fixture.ScalarAsync<string>($"SELECT body FROM captured_messages WHERE recipient = '{SecondDoctor.Email}' AND subject LIKE '%needs attention'"))!
            .ShouldContain("Reason: SLMC number not found");
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM admin_notifications WHERE kind = 'doctor_application_submitted'")).ShouldBe(2);
    }

    [Fact]
    public async Task Failed_sends_back_off_then_give_up_without_storing_the_recipient()
    {
        await using var failing = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IEmailSender, FailingEmailSender>()));
        await using (var scope = failing.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<INotificationService>().EnqueueAsync(
                new NotificationRecipient("nimal@example.com", null, Language.Si), new DoctorRunningLateModel("Nimal Perera"), "failing-test", Ct);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        }

        var state = "SELECT concat_ws('|', status, attempts, last_error) FROM notifications";
        async Task DispatchAsync()
        {
            await using var scope = failing.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<NotificationDispatcherJob>().RunAsync(Ct);
        }

        await DispatchAsync();
        (await Fixture.ScalarAsync<string>(state)).ShouldBe("pending|1|SMTP 550 mailbox [recipient] unavailable, call [phone]");
        await DispatchAsync();
        (await Fixture.ScalarAsync<string>(state))!.ShouldStartWith("pending|1|");

        for (var attempt = 0; attempt < PlatformPolicy.NotificationBackoff.Count; attempt++)
        {
            Factory.Time.Advance(PlatformPolicy.NotificationBackoff[attempt]);
            await DispatchAsync();
            (await Fixture.ScalarAsync<string>(state))!.ShouldStartWith(attempt < PlatformPolicy.NotificationBackoff.Count - 1 ? $"pending|{attempt + 2}|" : $"failed|{attempt + 2}|");
        }

        Factory.Time.Advance(TimeSpan.FromDays(1));
        await DispatchAsync();
        (await Fixture.ScalarAsync<string>(state))!.ShouldStartWith($"failed|{PlatformPolicy.NotificationBackoff.Count + 1}|");
        (await Fixture.ScalarAsync<string>("SELECT locale FROM notifications")).ShouldBe("si");
    }

    [Fact]
    public async Task Reminders_are_sent_once_per_start_time_and_again_after_a_reschedule()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var start = await Factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(30));
        var booked = await patient.PaidAsync(doctor.DoctorId, start);

        await Factory.RunJobAsync<RemindersJob>();
        (await KeysAsync("reminder_24h")).ShouldBeEmpty();

        Factory.Time.Advance(start - TimeSpan.FromHours(23) - Factory.Time.GetUtcNow());
        await Factory.RunJobAsync<RemindersJob>();
        await Factory.RunJobAsync<RemindersJob>();
        (await KeysAsync("reminder_24h")).ShouldBe([$"appt:{booked.Id}:{start:O}:r24:email"]);

        var moved = start - TimeSpan.FromHours(2);
        var doctorClient = await Factory.ClientForAsync(doctor.UserId);
        var request = await doctorClient.ProposedAsync(booked.Id, moved);
        (await (await Factory.ClientForAsync(patient.UserId)).PostAsync($"/api/v1/reschedule-requests/{request.Id}/accept", null, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        await Factory.RunJobAsync<RemindersJob>();
        (await KeysAsync("reminder_24h")).ShouldBe([$"appt:{booked.Id}:{moved:O}:r24:email", $"appt:{booked.Id}:{start:O}:r24:email"], ignoreOrder: true);

        Factory.Time.Advance(moved - TimeSpan.FromMinutes(50) - Factory.Time.GetUtcNow());
        await Factory.RunJobAsync<RemindersJob>();
        await Factory.RunJobAsync<RemindersJob>();
        (await KeysAsync("reminder_1h")).ShouldBe([$"appt:{booked.Id}:{moved:O}:r1:email"]);
        (await KeysAsync("reminder_24h")).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Test_appointments_produce_no_notifications()
    {
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        var now = Factory.Time.GetUtcNow();
        var test = await InsertConfirmedAsync(doctor.DoctorId, patient.UserId, now.AddHours(20), isTest: true);
        var real = await InsertConfirmedAsync(doctor.DoctorId, patient.UserId, now.AddHours(21), isTest: false);

        await Factory.RunJobAsync<RemindersJob>();
        (await doctor.Client.PostAsync($"/api/v1/appointments/{test}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await doctor.Client.PostAsync($"/api/v1/appointments/{real}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM notifications WHERE dedupe_key LIKE 'appt:{test}:%'")).ShouldBe(0);
        (await Fixture.ScalarAsync<long>($"SELECT count(*) FROM notifications WHERE dedupe_key LIKE 'appt:{real}:%'")).ShouldBe(2);
    }

    [Fact]
    public async Task Admin_inbox_lists_counts_and_marks_items_read()
    {
        await Factory.SubmitApplicationAsync();
        await Factory.SubmitApplicationAsync(SecondDoctor);
        var admin = await Factory.AdminClientAsync(AdminRole.Support);

        var unread = await (await admin.GetAsync("/api/v1/admin/notifications?unreadOnly=true", Ct))
            .ReadAsync<TeleMed.Application.Common.PagedResult<TeleMed.Application.Admin.Notifications.AdminNotificationDto>>();
        unread.Total.ShouldBe(2);
        unread.Items[0].Kind.ShouldBe(AdminNotificationKind.DoctorApplicationSubmitted);
        unread.Items[0].Href.ShouldStartWith("/doctor-applications/");
        (await CountAsync(admin)).ShouldBe(2);

        var read = await (await admin.PostAsync($"/api/v1/admin/notifications/{unread.Items[0].Id}/read", null, Ct))
            .ReadAsync<TeleMed.Application.Admin.Notifications.AdminNotificationDto>();
        read.ReadAt.ShouldNotBeNull();
        (await CountAsync(admin)).ShouldBe(1);
        (await admin.PostAsync($"/api/v1/admin/notifications/{Guid.NewGuid()}/read", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await admin.PostAsync("/api/v1/admin/notifications/read-all", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountAsync(admin)).ShouldBe(0);
        var all = await (await admin.GetAsync("/api/v1/admin/notifications?page=1&pageSize=1", Ct))
            .ReadAsync<TeleMed.Application.Common.PagedResult<TeleMed.Application.Admin.Notifications.AdminNotificationDto>>();
        (all.Total, all.Items.Count).ShouldBe((2, 1));
    }

    [Fact]
    public async Task Housekeeping_purges_old_auth_rows_and_captured_mail_and_clears_old_bodies()
    {
        await Factory.SignInWithPhoneAsync();
        var doctor = await Factory.BookableDoctorAsync();
        var patient = await Factory.PatientAsync();
        await patient.PaidAsync(doctor.DoctorId, LocalTime(Factory.Today().AddDays(2), 10));
        await Factory.RunJobAsync<NotificationDispatcherJob>();

        Factory.Time.Advance(TimeSpan.FromDays(6));
        await Factory.RunJobAsync<HousekeepingJob>();
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM otp_challenges")).ShouldBe(1);

        Factory.Time.Advance(TimeSpan.FromDays(85));
        await Factory.RunJobAsync<HousekeepingJob>();

        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM otp_challenges")).ShouldBe(0);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM refresh_tokens")).ShouldBe(0);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM captured_messages")).ShouldBe(0);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM notifications WHERE status = 'sent' AND body IS NOT NULL")).ShouldBe(0);
        (await Fixture.ScalarAsync<long>("SELECT count(*) FROM notifications WHERE status = 'sent'")).ShouldBeGreaterThan(0);
    }

    private async Task<long> CapturedAsync(string recipient) =>
        await Fixture.ScalarAsync<long>($"SELECT count(*) FROM captured_messages WHERE recipient = '{recipient}'");

    private async Task<List<string>> KeysAsync(string templateKey)
    {
        await using var connection = await Fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT dedupe_key FROM notifications WHERE template_key = '{templateKey}' ORDER BY dedupe_key";
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var keys = new List<string>();
        while (await reader.ReadAsync(Ct))
        {
            keys.Add(reader.GetString(0));
        }

        return keys;
    }

    private static async Task<long> CountAsync(HttpClient admin) =>
        (await (await admin.GetAsync("/api/v1/admin/notifications/unread-count", Ct)).ReadAsync<TeleMed.Application.Admin.Notifications.UnreadCountDto>()).Count;

    private async Task<Guid> InsertConfirmedAsync(Guid doctorId, Guid patientId, DateTimeOffset startAt, bool isTest)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var appointment = new Appointment
        {
            PatientId = patientId,
            DoctorId = doctorId,
            StartAt = startAt,
            EndAt = startAt.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            IsTest = isTest,
            VisitPatientName = "Kamal Silva",
            VisitPatientDateOfBirth = new DateOnly(1990, 4, 1),
            ConfirmedAt = startAt.AddDays(-3),
        };
        scope.ServiceProvider.GetRequiredService<IAppointmentRepository>().Add(appointment);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return appointment.Id;
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        public bool IsEnabled => true;

        public Task SendAsync(string to, string subject, string body, CancellationToken ct, IReadOnlyList<EmailAttachment>? attachments = null) =>
            throw new InvalidOperationException($"SMTP 550 mailbox {to} unavailable, call +94 77 123 4567");
    }
}
