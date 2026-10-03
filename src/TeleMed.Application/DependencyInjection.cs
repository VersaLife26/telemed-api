using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Application.Admin.AdminUsers;
using TeleMed.Application.Admin.Appointments;
using TeleMed.Application.Admin.Audit;
using TeleMed.Application.Admin.Content;
using TeleMed.Application.Admin.Credentialing;
using TeleMed.Application.Admin.Disputes;
using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Admin.Users;
using TeleMed.Application.Analytics;
using TeleMed.Application.Admin.Doctors;
using TeleMed.Application.Admin.Notifications;
using TeleMed.Application.Appointments;
using TeleMed.Application.Auth;
using TeleMed.Application.ClinicalNotes;
using TeleMed.Application.Consultations;
using TeleMed.Application.CustomerCare;
using TeleMed.Application.DoctorApplications;
using TeleMed.Application.Doctors;
using TeleMed.Application.Jobs;
using TeleMed.Application.MedicalReports;
using TeleMed.Application.Notifications;
using TeleMed.Application.Payments;
using TeleMed.Application.Payouts;
using TeleMed.Application.Prescriptions;
using TeleMed.Application.Reference;
using TeleMed.Application.Reschedules;
using TeleMed.Application.Scheduling;
using TeleMed.Application.Testing;
using TeleMed.Application.Users;
using TeleMed.Application.Vault;

namespace TeleMed.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<ReferenceDataService>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<SessionActivity>();
        services.AddScoped<AuthService>();
        services.AddScoped<MeService>();
        services.AddScoped<CapturedMessagesService>();
        services.AddScoped<AdminUserService>();
        services.AddScoped<ContentService>();
        services.AddScoped<DoctorApplicationService>();
        services.AddScoped<CredentialingService>();
        services.AddScoped<AdminDoctorService>();
        services.AddScoped<DoctorSelfService>();
        services.AddScoped<DoctorDirectoryService>();
        services.AddScoped<ScheduleService>();
        services.AddScoped<HolidayService>();
        services.AddScoped<SlotBlockService>();
        services.AddScoped<SlotService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<AppointmentNotifier>();
        services.AddScoped<AdminNotificationService>();
        services.AddScoped<PaymentLifecycle>();
        services.AddScoped<CalendarClearance>();
        services.AddScoped<RescheduleService>();
        services.AddScoped<AdminAppointmentService>();
        services.AddScoped<AppointmentService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<ConsultationService>();
        services.AddScoped<ConsultationSignalService>();
        services.AddScoped<InstantMeetingService>();
        services.AddScoped<VaultAccessPolicy>();
        services.AddScoped<VaultService>();
        services.AddScoped<ClinicalNoteService>();
        services.AddScoped<PrescriptionService>();
        services.AddScoped<MedicalReportService>();
        services.AddScoped<ExpireUnpaidBookingsJob>();
        services.AddScoped<PaymentSettlementJob>();
        services.AddScoped<ExpireRescheduleRequestsJob>();
        services.AddScoped<NotificationDispatcherJob>();
        services.AddScoped<RemindersJob>();
        services.AddScoped<HousekeepingJob>();
        services.AddScoped<ConsultationSweepJob>();
        services.AddScoped<AutoCompleteAppointmentsJob>();
        services.AddScoped<PayoutService>();
        services.AddScoped<FinanceService>();
        services.AddScoped<AdminRefundService>();
        services.AddScoped<PromoCodeService>();
        services.AddScoped<DisputeService>();
        services.AddScoped<CustomerCareService>();
        services.AddScoped<PlatformUserService>();
        services.AddScoped<AuditService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<DoctorInsightsService>();
        services.AddScoped<DailyPayoutsJob>();
        services.AddScoped<UserErasureJob>();
        return services;
    }
}
