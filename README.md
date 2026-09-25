# TeleMed API

ASP.NET Core (.NET 10) backend for the VersaLife telemedicine platform. It runs as one process with one PostgreSQL database, and every write is visible on the next read. It replaces the Go `telemed-backend`. See `docs/frontend-contract.md` for the client migration guide and `openapi/v1.json` for the full contract.

## Architecture

```
src/
  TeleMed.Domain/          entities (one per table), enums, pure rules (SlotPlanner, CancellationPolicy,
                           CommissionCalculator, PlatformPolicy constants, ...). No I/O.
  TeleMed.Application/     one folder per feature: service, DTOs, FluentValidation validators, Mapperly mappers,
                           repository interfaces. Abstractions/ for everything external (IUnitOfWork, IFileStorage,
                           IPaymentProvider, ISmsSender, ...). Jobs/ holds the background job bodies as plain services.
  TeleMed.Infrastructure/  EF Core (AppDbContext, configurations, migrations, repositories, interceptors), Identity,
                           messaging, payments, storage, TURN, PDF, the PeriodicJob host and all options + validation.
  TeleMed.Api/             controllers, the SignalR consultation hub, auth schemes, rate limiting, ProblemDetails, OpenAPI.
tests/
  TeleMed.Domain.Tests/            pure rule tests
  TeleMed.Api.IntegrationTests/    WebApplicationFactory + Testcontainers Postgres + Respawn + FakeTimeProvider
```

The dependencies point inward: Api → Application ← Infrastructure, and everything → Domain.

**Rules**
- Controllers only translate HTTP into one service call; they hold no logic.
- Services never reference EF Core. Repositories are the only database code. They stage changes, and services commit through `IUnitOfWork` (`BeginTransactionAsync` when they need locks).
- Entities never leave Application. Responses are DTOs.
- Errors are exceptions (`BadRequest`, `NotFound`, `Conflict`, `Forbidden`, ...), and `GlobalExceptionHandler` turns them into ProblemDetails.
- No behaviour branches on `IHostEnvironment`. Each third-party dependency has its own switch (below).
- `TimeProvider` is injected everywhere.
- Money is `long` cents plus a currency.
- Enums are stored as snake_case strings and serialized as camelCase strings.
- Optimistic concurrency uses `xmin` (409 on conflict). Calendar writes are serialised per doctor with an advisory lock, and exclusion constraints are the final guard against double booking.

## Running locally

Requirements: .NET SDK 10.0.100+, Docker, and Node 22+ (only for the smoke script).

```bash
docker compose up -d --build --wait     # Postgres on 127.0.0.1:5433, API on http://127.0.0.1:8080
open http://127.0.0.1:8080/scalar       # interactive API reference
```

`docker-compose.yml` runs with the Capture SMS/email senders, Mock payments, both test endpoints, and local admin JWTs (`admin@telemed.local` is bootstrapped as super_admin). The API logs a **TEST SWITCHES ENABLED** warning at startup.

To run against the compose database from your IDE, use `dotnet run --project src/TeleMed.Api` with `ASPNETCORE_ENVIRONMENT=Development`. Point `Database:ConnectionString` at port 5433 first (`Host=localhost;Port=5433;Database=telemed;Username=telemed;Password=telemed;GSS Encryption Mode=Disable`), because `appsettings.Development.json` expects 5432.

```bash
dotnet build -warnaserror
dotnet test                             # needs Docker (Testcontainers)
```

**OpenAPI.** `openapi/v1.json` is checked in, and `OpenApiDocumentTests` fails when it is stale. To regenerate it:

```bash
UPDATE_OPENAPI=1 dotnet test --project tests/TeleMed.Api.IntegrationTests --filter-class "*OpenApiDocumentTests"
```

**Migrations**

```bash
dotnet tool restore
dotnet ef migrations add <Name> -p src/TeleMed.Infrastructure -s src/TeleMed.Infrastructure -o Persistence/Migrations
```

## Configuration switches

All options are validated at startup (`ValidateOnStart`), so a switch that is on without its credentials stops the app. Environment variables use `__` in place of `:`. Rows marked **test** are listed in the startup warning banner and must be off in production.

| Key | Default | Test | Notes |
|---|---|---|---|
| `Database:ConnectionString` | — | | Required, e.g. `Host=db;Port=5432;Database=telemed;Username=telemed;Password=…;GSS Encryption Mode=Disable`. Keep `GSS Encryption Mode=Disable` unless you use Kerberos: the image has no libgssapi, and Npgsql otherwise logs an error on every connect. |
| `Database:MigrateOnStartup` | false | | Applies EF migrations before serving. |
| `Auth:Jwt:SigningKey` | — | | Required, 32+ bytes. `Issuer`, `Audience`, `AccessTokenLifetime` (15m), `RefreshTokenLifetime` (7d) and `SessionCacheDuration` (30s) can also be set. |
| `Auth:Google:ClientIds` | [] | | An empty list turns Google sign-in off. |
| `Otp:HmacKey` | — | | Required, 32+ bytes. |
| `Otp:FixedCode` | null | **test** | A six-digit code accepted for every OTP. |
| `Sms:Provider` | None | Capture = **test** | `Dialog` needs `Sms:Dialog:{BaseUrl,ApplicationId,Password}`. `Capture` writes to `captured_messages`. |
| `Email:Provider` | None | Capture = **test** | `Smtp` needs `Email:FromAddress` and `Email:Smtp:{Host,Port,Security,Username,Password}`. |
| `Payments:PayHere:Enabled` | false | | Needs `MerchantId`, `MerchantSecret`, `AppId`, `AppSecret`, `BaseUrl`, `NotifyUrl`, `ReturnUrl` and `CancelUrl`. |
| `Payments:Mock:Enabled` | false | **test** | Enables provider `mock` and `POST /payments/{id}/mock/complete`. |
| `Payments:Mock:AutoSucceed` | false | **test** | Mock intents succeed immediately. Requires `Mock:Enabled`. |
| `Turn:Provider` | None | | `Static` uses `Turn:StaticUrls`, `Username` and `Credential`. `Cloudflare` uses `Turn:Cloudflare:{KeyId,ApiToken}`. `Turn:StunUrls` is always served. |
| `Video:RoomTokenKey` | — | | Required, 32+ bytes. `RoomTokenLifetime` defaults to 10m. |
| `Storage:RootPath` / `Storage:SigningKey` | — | | Required. The file root (`/var/lib/telemed/files` in the image) and the key for signed `/files/{token}` URLs. `UrlTtl` defaults to 5m. |
| `Crypto:BankDataKey` | — | | Required. A base64 32-byte AES key for doctors' bank account numbers. |
| `Prescriptions:HmacKey` / `Prescriptions:VerifyBaseUrl` | — | | Required. The QR code encodes `{VerifyBaseUrl}/p/{id}?h={hmac}`. |
| `AppLinks:PatientAppUrl` | "" | | The base for links in notifications. |
| `AdminAuth:CloudflareAccess:Enabled` | true | | Needs `TeamDomain` and `Audience`. |
| `AdminAuth:LocalJwt:Enabled` | false | **test** | HS256 admin tokens (`iss telemed-admin-local`, `aud telemed-admin`, `email` claim) signed with `AdminAuth:LocalJwt:SigningKey`. |
| `AdminAuth:IpAllowlist` | [] | | IPs or CIDRs. An empty list denies every admin request. |
| `AdminAuth:AllowAnyIp` | false | **test** | Skips the allowlist. |
| `AdminAuth:BootstrapSuperAdminEmail` | null | | Ensures this email is an active super_admin at startup. |
| `AdminAuth:CacheDuration` | 30s | | Cache for admin-user lookups. |
| `Testing:SharedSecret` | "" | | Must be sent as `X-Test-Secret` to the test endpoints. |
| `Testing:CaptureInbox:Enabled` | false | **test** | `/api/v1/test/captured-messages` (404 when off). |
| `Testing:InstantMeetings:Enabled` | false | **test** | `POST /api/v1/test/instant-meetings` (404 when off). |
| `Jobs:<Name>:Enabled` / `Jobs:<Name>:Interval` | true / see below | | Per job. |
| `RateLimiting:<Policy>:{PermitLimit,Window}` | see `appsettings.json` | | Fixed window per client IP. |
| `Cors:Origins` / `Cors:AdminOrigins` | [] | | Exact origins. Admin origins also satisfy the admin Origin check. |
| `ForwardedHeaders:TrustCloudflare` | false | | Takes the client IP from `CF-Connecting-IP`. Enable only when the origin is reachable solely through Cloudflare. |

## Background jobs

Each job runs on its own `PeriodicTimer` inside a Postgres advisory lock, so a second instance skips a job that is already running. Test appointments are ignored by every job.

| Job | Default interval | Work |
|---|---|---|
| ExpireUnpaidBookings | 1 min | Cancels `pending_payment` bookings past their due time, fails the payment and releases the promo. |
| ExpireRescheduleRequests | 1 min | Treats a request whose original start has passed as declined: cancels with a 100% refund. |
| ConsultationSweep | 1 min | Patient absent at the end → no_show. Doctor absent → cancelled with a 100% refund plus an admin inbox item. Doctor running late → the next patient is notified. Stale for 2h → ended. |
| AutoCompleteAppointments | 15 min | Marks confirmed appointments completed once their end is more than 12h past. |
| PaymentSettlement | 5 min | Captures or voids authorized payments (a partial capture after a late cancel), sends approved refunds to the provider, and releases expired promo holds. |
| NotificationDispatcher | 10 s, plus a wake-up after commit | Sends queued email/SMS in `FOR UPDATE SKIP LOCKED` batches. Backoff is 30s, 2m, 10m, 1h, 6h, then failed. |
| Reminders | 5 min | Sends 24h and 1h reminders, deduplicated by key. |
| DailyPayouts | 1 h (acts once after 02:00 Asia/Colombo) | Builds yesterday's batch. A payment is payable once its appointment is completed or no_show and both the capture and that outcome are more than 24h old, with no refund in flight. Clawbacks from refunds that settle after payout are deducted, and a shortfall carries forward. Re-running is a no-op. |
| UserErasure | 1 day | Anonymises accounts 30 days after deletion, deletes the photo and revokes tokens. Clinical records are kept. |
| Housekeeping | 1 day | Purges expired refresh tokens, OTP challenges and old captured messages. |

## End-to-end smoke flow

`scripts/smoke.sh` builds the image, starts the compose stack (Postgres plus the API, with migrations applied on startup), waits for `/health/ready`, and runs `scripts/smoke.mjs`. That script uses Node's built-in fetch and WebSocket, so there is nothing to install. Pass `--down` to remove the stack and its volumes afterwards.

| # | Step |
|---|---|
| 0 | `GET /health/ready` returns 200. |
| 1 | Register a patient via OTP (`/auth/otp/send`). The code is read from `/test/captured-messages/latest`, then `/auth/otp/verify` is called. |
| 2 | Submit a doctor application, approve it with a local admin JWT, and sign in as the doctor. |
| 3 | The doctor PUTs `/doctors/me/schedule`, and `GET /doctors/{id}/slots` shows free slots in the very next request. |
| 4 | Book the first free slot, create a `mock` intent and complete it. The appointment becomes `confirmed`. |
| 5 | The doctor creates an instant test meeting. Both sides join, connect two SignalR clients to `/hubs/consultation`, and relay an offer and an answer. A third connection replaces the old one. |
| 6 | On the paid appointment: admit, finalise a clinical note, upload a signature and seal, issue a prescription, download its PDF (rendered by QuestPDF inside the container), and verify it (a tampered HMAC fails). |
| 7 | Perf check: `GET /slots` over 31 days for one doctor with 15-minute slots all day (2,880 slots), p50 under 50 ms. |
