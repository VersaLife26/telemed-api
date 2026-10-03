using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Admin.AdminUsers;
using TeleMed.Application.Admin.Audit;
using TeleMed.Application.Admin.Content;
using TeleMed.Application.Admin.Disputes;
using TeleMed.Application.Admin.Finance;
using TeleMed.Application.Admin.Notifications;
using TeleMed.Application.Admin.Users;
using TeleMed.Application.Analytics;
using TeleMed.Application.Appointments;
using TeleMed.Application.Auth;
using TeleMed.Application.ClinicalNotes;
using TeleMed.Application.Consultations;
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
using TeleMed.Application.Vault;
using TeleMed.Domain.Entities;
using TeleMed.Infrastructure.BackgroundJobs;
using TeleMed.Infrastructure.Crypto;
using TeleMed.Infrastructure.Identity;
using TeleMed.Infrastructure.Messaging;
using TeleMed.Infrastructure.Options;
using TeleMed.Infrastructure.Payments;
using TeleMed.Infrastructure.Pdf;
using TeleMed.Infrastructure.Persistence;
using TeleMed.Infrastructure.Persistence.Interceptors;
using TeleMed.Infrastructure.Persistence.Repositories;
using TeleMed.Infrastructure.Storage;
using TeleMed.Infrastructure.Video;

namespace TeleMed.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<TimestampInterceptor>();
        services.AddScoped<AuditInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            AppDbContext.Configure(options, sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString);
            options.AddInterceptors(sp.GetRequiredService<TimestampInterceptor>(), sp.GetRequiredService<AuditInterceptor>());
        });
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Hosted services start in registration order, so migrations finish before any background job's first tick.
        services.AddHostedService<DatabaseMigrator>();
        services.AddHostedService<AdminBootstrapper>();
        services.AddHostedService<TestSwitchReporter>();

        services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IOtpChallengeRepository, OtpChallengeRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<ICapturedMessageRepository, CapturedMessageRepository>();
        services.AddScoped<IAdminUserRepository, AdminUserRepository>();
        services.AddScoped<IContentRepository, ContentRepository>();
        services.AddScoped<IDoctorApplicationRepository, DoctorApplicationRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<IDoctorDocumentRepository, DoctorDocumentRepository>();
        services.AddScoped<ISchedulingRepository, SchedulingRepository>();
        services.AddScoped<ICalendarLock, CalendarLock>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IRescheduleRepository, RescheduleRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAdminNotificationRepository, AdminNotificationRepository>();
        services.AddScoped<IConsultationRepository, ConsultationRepository>();
        services.AddScoped<IClinicalNoteRepository, ClinicalNoteRepository>();
        services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
        services.AddScoped<IMedicalReportRepository, MedicalReportRepository>();
        services.AddScoped<IVaultRepository, VaultRepository>();
        services.AddScoped<IRecordAccessRepository, RecordAccessRepository>();
        services.AddScoped<IPayoutRepository, PayoutRepository>();
        services.AddScoped<IFinanceRepository, FinanceRepository>();
        services.AddScoped<IDisputeRepository, DisputeRepository>();
        services.AddScoped<IPlatformUserRepository, PlatformUserRepository>();
        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();

        services.AddIdentityServices(configuration);
        services.AddAdminAuth(configuration);
        services.AddMessaging(configuration);
        services.AddStorage(configuration);
        services.AddPayments(configuration);
        services.AddVideo(configuration);
        services.AddPrescriptions(configuration);
        services.AddJob<ExpireUnpaidBookingsJob>(configuration, TimeSpan.FromMinutes(1));
        services.AddJob<PaymentSettlementJob>(configuration, TimeSpan.FromMinutes(5));
        services.AddJob<ExpireRescheduleRequestsJob>(configuration, TimeSpan.FromMinutes(1));
        services.AddSingleton<JobWakeup<NotificationDispatcherJob>>();
        services.AddJob<NotificationDispatcherJob>(configuration, TimeSpan.FromSeconds(10));
        services.AddJob<RemindersJob>(configuration, TimeSpan.FromMinutes(5));
        services.AddJob<HousekeepingJob>(configuration, TimeSpan.FromDays(1));
        services.AddJob<ConsultationSweepJob>(configuration, TimeSpan.FromMinutes(1));
        services.AddJob<AutoCompleteAppointmentsJob>(configuration, TimeSpan.FromMinutes(15));
        services.AddJob<DailyPayoutsJob>(configuration, TimeSpan.FromHours(1));
        services.AddJob<UserErasureJob>(configuration, TimeSpan.FromDays(1));

        services.AddOptions<CryptoOptions>()
            .Bind(configuration.GetSection(CryptoOptions.Section))
            .Validate(o => o.BankDataKeyBytes() is not null, $"Crypto:BankDataKey must be a base64-encoded {CryptoOptions.KeyBytes}-byte key.")
            .ValidateOnStart();
        services.AddSingleton<IBankDataCipher, AesGcmBankDataCipher>();

        services.AddOptions<TestingOptions>()
            .Bind(configuration.GetSection(TestingOptions.Section))
            .Validate(
                o => !o.AnyEnabled || o.SharedSecret.Length >= TestingOptions.MinSecretLength,
                $"Testing:SharedSecret must be at least {TestingOptions.MinSecretLength} characters when any Testing endpoint is enabled.")
            .ValidateOnStart();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: ["ready"]);

        return services;
    }

    private static void AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= JwtOptions.MinKeyBytes,
                $"Auth:Jwt:SigningKey must be at least {JwtOptions.MinKeyBytes} bytes.")
            .Validate(o => o.AccessTokenLifetime > TimeSpan.Zero && o.RefreshTokenLifetime > o.AccessTokenLifetime,
                "Auth:Jwt lifetimes must be positive and the refresh token must outlive the access token.")
            .ValidateOnStart();
        services.AddOptions<GoogleAuthOptions>().Bind(configuration.GetSection(GoogleAuthOptions.Section));
        services.AddOptions<OtpOptions>()
            .Bind(configuration.GetSection(OtpOptions.Section))
            .Validate(o => Encoding.UTF8.GetByteCount(o.HmacKey) >= 32, "Otp:HmacKey must be at least 32 bytes.")
            .Validate(o => string.IsNullOrEmpty(o.FixedCode) || (o.FixedCode.Length == 6 && o.FixedCode.All(char.IsAsciiDigit)),
                "Otp:FixedCode must be exactly 6 digits.")
            .ValidateOnStart();

        services.AddIdentityCore<User>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredUniqueChars = 1;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddUserStore<NonSavingUserStore>();

        services.AddMemoryCache();
        services.AddSingleton<SessionValidator>();
        services.AddScoped<IUserAccounts, UserAccounts>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddSingleton<IOtpCodes, OtpCodes>();
    }

    private static void AddAdminAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AdminAuthOptions>()
            .Bind(configuration.GetSection(AdminAuthOptions.Section))
            .Validate(o => !o.CloudflareAccess.Enabled || (
                    Uri.CheckHostName(o.CloudflareAccess.Host) == UriHostNameType.Dns
                    && o.CloudflareAccess.Audience.Length > 0),
                "AdminAuth:CloudflareAccess:Enabled requires AdminAuth:CloudflareAccess:TeamDomain and Audience.")
            .Validate(o => !o.LocalJwt.Enabled || o.LocalJwt.KeyBytes.Length >= JwtOptions.MinKeyBytes,
                $"AdminAuth:LocalJwt:SigningKey must be at least {JwtOptions.MinKeyBytes} bytes when LocalJwt is enabled.")
            .Validate(o => o.TryParseAllowlist(out _), "AdminAuth:IpAllowlist entries must be IP addresses or CIDR ranges.")
            .Validate(o => string.IsNullOrWhiteSpace(o.BootstrapSuperAdminEmail) || MailAddress.TryCreate(o.BootstrapSuperAdminEmail.Trim(), out _),
                "AdminAuth:BootstrapSuperAdminEmail must be an email address.")
            .Validate(o => o.CacheDuration > TimeSpan.Zero, "AdminAuth:CacheDuration must be positive.")
            .ValidateOnStart();
        services.AddSingleton<IAdminDirectory, AdminDirectory>();
    }

    private static void AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmsOptions>()
            .Bind(configuration.GetSection(SmsOptions.Section))
            .Validate(o => o.Provider != SmsProvider.Dialog || (
                    Uri.TryCreate(o.Dialog.BaseUrl, UriKind.Absolute, out _)
                    && o.Dialog.ApplicationId.Length > 0
                    && o.Dialog.Password.Length > 0),
                "Sms:Provider=Dialog requires Sms:Dialog:BaseUrl, ApplicationId and Password.")
            .ValidateOnStart();
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.Section))
            .Validate(o => o.Provider != EmailProvider.Smtp || (o.Smtp.Host.Length > 0 && o.FromAddress.Length > 0),
                "Email:Provider=Smtp requires Email:FromAddress and Email:Smtp:Host.")
            .ValidateOnStart();

        services.AddHttpClient<DialogSmsSender>((sp, http) =>
            http.Timeout = sp.GetRequiredService<IOptions<SmsOptions>>().Value.Dialog.Timeout);
        services.AddScoped<ISmsSender>(sp => sp.GetRequiredService<IOptions<SmsOptions>>().Value.Provider switch
        {
            SmsProvider.Dialog => sp.GetRequiredService<DialogSmsSender>(),
            SmsProvider.Capture => ActivatorUtilities.CreateInstance<CapturingSender>(sp),
            _ => new DisabledSender(),
        });
        services.AddScoped<IEmailSender>(sp => sp.GetRequiredService<IOptions<EmailOptions>>().Value.Provider switch
        {
            EmailProvider.Smtp => ActivatorUtilities.CreateInstance<SmtpEmailSender>(sp),
            EmailProvider.Capture => ActivatorUtilities.CreateInstance<CapturingSender>(sp),
            _ => new DisabledSender(),
        });
    }

    private static void AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PaymentsOptions>()
            .Bind(configuration.GetSection(PaymentsOptions.Section))
            .Validate(o => o.PayHere.IsValid(),
                "Payments:PayHere:Enabled requires MerchantId, MerchantSecret, AppId, AppSecret and absolute BaseUrl, NotifyUrl, ReturnUrl and CancelUrl.")
            .Validate(o => !o.Mock.AutoSucceed || o.Mock.Enabled, "Payments:Mock:AutoSucceed requires Payments:Mock:Enabled.")
            .ValidateOnStart();
        services.AddHttpClient<PayHereProvider>((sp, http) =>
            http.Timeout = sp.GetRequiredService<IOptions<PaymentsOptions>>().Value.PayHere.Timeout);
        services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<PayHereProvider>());
        services.AddScoped<IPayHereWebhookVerifier>(sp => sp.GetRequiredService<PayHereProvider>());
        services.AddScoped<IPaymentProvider, MockPaymentProvider>();
    }

    private static void AddVideo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<VideoOptions>()
            .Bind(configuration.GetSection(VideoOptions.Section))
            .Validate(o => o.KeyBytes.Length >= VideoOptions.MinKeyBytes, $"Video:RoomTokenKey must be at least {VideoOptions.MinKeyBytes} bytes.")
            .Validate(o => o.RoomTokenLifetime > TimeSpan.Zero, "Video:RoomTokenLifetime must be positive.")
            .ValidateOnStart();
        services.AddOptions<TurnOptions>()
            .Bind(configuration.GetSection(TurnOptions.Section))
            .Validate(o => o.IsValid(),
                "Turn:Provider=Static requires turn:/turns: Turn:StaticUrls and both or neither of Username and Credential; "
                + "Turn:Provider=Cloudflare requires Turn:Cloudflare:KeyId, ApiToken and a positive TtlSeconds.")
            .ValidateOnStart();
        services.AddOptions<AppLinksOptions>()
            .Bind(configuration.GetSection(AppLinksOptions.Section))
            .Validate(o => o.IsValid(), "AppLinks:PatientAppUrl and AppLinks:DoctorAppUrl must be absolute http(s) URLs when set.")
            .ValidateOnStart();

        services.AddSingleton<IRoomTokens, HmacRoomTokens>();
        services.AddSingleton<IAppLinks, AppLinks>();
        services.AddHttpClient(CloudflareTurnProvider.HttpClientName, (sp, http) =>
            http.Timeout = sp.GetRequiredService<IOptions<TurnOptions>>().Value.Cloudflare.Timeout);
        services.AddSingleton<CloudflareTurnProvider>();
        services.AddSingleton<ITurnCredentialProvider>(sp => sp.GetRequiredService<IOptions<TurnOptions>>().Value.Provider == TurnProvider.Cloudflare
            ? sp.GetRequiredService<CloudflareTurnProvider>()
            : ActivatorUtilities.CreateInstance<StaticTurnProvider>(sp));
    }

    private static void AddPrescriptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PrescriptionOptions>()
            .Bind(configuration.GetSection(PrescriptionOptions.Section))
            .Validate(o => o.KeyBytes.Length >= PrescriptionOptions.MinKeyBytes, $"Prescriptions:HmacKey must be at least {PrescriptionOptions.MinKeyBytes} bytes.")
            .Validate(o => o.HasValidVerifyBaseUrl(), "Prescriptions:VerifyBaseUrl must be an absolute http(s) URL.")
            .ValidateOnStart();

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        services.AddSingleton<IPrescriptionSigner, HmacPrescriptionSigner>();
        services.AddSingleton<IPrescriptionPdfRenderer, QuestPrescriptionPdfRenderer>();
        services.AddSingleton<IMedicalReportSigner, HmacMedicalReportSigner>();
        services.AddSingleton<IMedicalReportPdfRenderer, QuestMedicalReportPdfRenderer>();
    }

    private static void AddJob<TJob>(this IServiceCollection services, IConfiguration configuration, TimeSpan defaultInterval)
        where TJob : IBackgroundJob
    {
        var name = PeriodicJob<TJob>.Name;
        services.AddOptions<JobOptions>(name)
            .Configure(o => o.Interval = defaultInterval)
            .Bind(configuration.GetSection($"Jobs:{name}"))
            .Validate(o => o.Interval > TimeSpan.Zero, $"Jobs:{name}:Interval must be positive.")
            .ValidateOnStart();
        services.AddHostedService<PeriodicJob<TJob>>();
    }

    private static void AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.Section))
            .Validate(o => o.RootPath.Length > 0, "Storage:RootPath is required.")
            .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= 32, "Storage:SigningKey must be at least 32 bytes.")
            .Validate(o => o.UrlTtl > TimeSpan.Zero, "Storage:UrlTtl must be positive.")
            .ValidateOnStart();
        services.AddSingleton<IFileStorage, FileSystemStorage>();
    }
}
