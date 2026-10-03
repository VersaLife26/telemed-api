# Frontend migration guide: Go backend → TeleMed API (.NET)

This guide is for the `telemed-frontend` team. It lists what changes when the consumer app (`lib/consumer/*`) and the admin console (`lib/admin/api/endpoints.ts`) move to the new API. `openapi/v1.json` in this repo is the authoritative contract for every path, body and response, so generate types from it rather than copying shapes from here. An interactive reference is served at `/scalar`.

## 1. Global rules

| Topic | Go backend | New API |
|---|---|---|
| Base path | `/api/v1` | `/api/v1` (unchanged) |
| JSON casing | snake_case | **camelCase** everywhere: bodies, responses, query parameters |
| Envelope | `{data, meta}` (admin enforces it strictly) | **None.** The body *is* the resource. Remove `parseEnvelope` and the admin `MALFORMED_RESPONSE` check. |
| Lists | `?page=&per_page=` → `meta {page, per_page, total, total_pages}`; the consumer only sent `per_page` | `?page=1&pageSize=20` (1-based, `pageSize` 1..100) → `{items, page, pageSize, total}`. Compute `totalPages` client-side. A few short lists (slots, holidays, reschedule requests, chat) are plain arrays or objects; check the OpenAPI response type. |
| Enums | mixed (`AVAILABLE`, `confirmed`, ...) | camelCase strings, e.g. `pendingPayment`, `noShow`, `underReview`, `slmcCertificate` |
| Money | `*_cents`, plus a deprecated `fee_lkr` that was actually cents | `…Cents` (integer) plus `currency` (`LKR`). No `_lkr` fields. |
| Time | `start_at` UTC plus `start_at_local` | ISO-8601 UTC `DateTimeOffset` only. Format in the doctor's zone: `GET /doctors/{id}/slots` returns `timezone` (IANA, e.g. `Asia/Colombo`). |
| Dates | strings | `YYYY-MM-DD` (`DateOnly`) |
| Concurrency | `version` on some resources | Same idea. Send back the `version` you read (`/me`, clinical notes, schedules and others). A stale version returns `409 concurrency_conflict`. |
| Request id | `request_id` / `X-Request-ID` | `traceId` inside every problem response |

## 2. Errors: ProblemDetails (RFC 9457)

Every non-2xx response is `application/problem+json`:

```json
{ "type": "...", "title": "Conflict", "status": 409, "detail": "That time is no longer available.",
  "code": "slot_unavailable", "traceId": "00-…" }
```

- **`code`** is a stable lowercase snake_case reason for business errors (for example `slot_unavailable`, `patient_overlap`, `account_suspended`, `invalid_refresh_token`, `refresh_token_reused`, `join_window_closed`, `note_finalised`, `concurrency_conflict`, `ip_not_allowed`, `origin_not_allowed`). Branch on `code`, never on `detail`. It replaces the old uppercase codes (`SLOT_UNAVAILABLE`, `VALIDATION_FAILED`, ...). It is absent on plain 401, 403 and 404 responses.
- **Validation (400)** has no `code`. Its `errors` object maps camelCase field paths to messages: `{"errors": {"visitPatient.dateOfBirth": ["…"]}}`. This replaces `fields`. `readApplyError` should iterate over `errors`.
- **Message text** is in `detail` (previously `message`).
- Some conflicts add extension members. For example, a holiday that hits booked appointments returns `409 appointments_affected` with `affectedAppointments`, and an over-large refund returns `refundableCents`.
- **429** comes from the per-IP rate limits (OTP send/verify, login, doctor apply, uploads, eligibility, slots, prescription verify).

## 3. Patient and doctor auth

| Old | New |
|---|---|
| `POST /auth/otp/send {phone, purpose, language}` → `{request_id, expires_in, attempts_remaining}` | `POST /auth/otp/send {phone \| email, language?}` → `{channel, expiresIn}`. There is no `purpose`. The first OTP sign-in creates a patient account. |
| `POST /auth/otp/verify {phone, otp, purpose, device_id?}` | `POST /auth/otp/verify {phone \| email, code, language?}`. The field is now **`code`**. |
| `POST /auth/login/email`, `POST /auth/register/email {email, password, name}` | Same paths. Register takes `{email, password, fullName, language?}`. |
| — | `POST /auth/password/forgot {email}` → 204. Always the same response, whether or not the email exists. Sends a 30-minute link when the account is active. |
| — | `POST /auth/password/reset {token, newPassword}` → the usual token response. Invalid or expired links return `401 invalid_reset_token`. |
| `POST /auth/oauth/google {id_token, create_account}` | `POST /auth/google {idToken}`. It creates a patient account when the Google identity is unknown, so there is no flag. It returns 404 when Google sign-in is switched off. |
| `POST /auth/refresh {refresh_token}` → `{access_token, refresh_token}` | `POST /auth/refresh {refreshToken}` → the full token response below. |
| `POST /auth/logout {refresh_token}` | `POST /auth/logout {refreshToken}` → 204. `POST /auth/logout-all` (bearer) revokes every session. |

**Token response** (every auth endpoint):

```json
{ "accessToken": "…", "refreshToken": "…", "expiresIn": 900,
  "user": { "id", "role": "patient|doctor", "fullName", "email", "phoneNumber", "language", "dateOfBirth", "sex",
            "address", "allergies", "photoUrl", "hasPassword", "version" } }
```

- Renamed fields: `name` → `fullName`, `phone` → `phoneNumber`. `status` is not returned; a suspended account gets `403 account_suspended` at sign-in and refresh.
- Access tokens last 15 minutes. Refresh tokens **rotate** on every use. Reusing an old refresh token more than 30 seconds after it was rotated revokes the whole session family (`refresh_token_reused`). The BFF must store the new refresh cookie before it retries.
- Suspending or deleting a user invalidates live access tokens within about 30 seconds, and the API then returns 401.
- Doctors sign in the same way. Check `user.role === "doctor"`, as today.

## 4. Admin auth (Cloudflare Access)

- **Forward the Access JWT unchanged in the `Cf-Access-Jwt-Assertion` header**, or let the `CF_Authorization` cookie pass through. **Do not send it as `Authorization: Bearer`**: that header is reserved for the local development admin token, and a Cloudflare JWT sent there gets a 401.
- The JWT's `email` must match an active admin user. An unknown or inactive email gets 403. `GET /admin/me` → `{id, email, displayName, role, permissions[]}` (note `displayName`; an inactive admin gets 403 rather than `active: false`). `GET /admin/permissions` returns the permission matrix and replaces `admin-users/permissions`.
- **IP allowlist.** Admin requests from outside `AdminAuth:IpAllowlist` get `403` with `code: "ip_not_allowed"`, so detect this by `code` instead of probing `doctors/pending`. The API takes the client IP from `CF-Connecting-IP` when `ForwardedHeaders:TrustCloudflare` is on. With the BFF in between, the allowlist sees the BFF's egress IP unless the BFF's own proxy headers are trusted. Agree on the deployment topology before go-live.
- **CSRF.** Unsafe admin methods need an `Origin` listed in `Cors:AdminOrigins`, or `Sec-Fetch-Site: same-origin`; otherwise the response is `403 origin_not_allowed`. The BFF already forwards `Origin`.
- **Local development:** `AdminAuth:LocalJwt` accepts an HS256 `Authorization: Bearer` token with `iss=telemed-admin-local`, `aud=telemed-admin` and an `email` claim. See `scripts/smoke.mjs` for how to mint one.

## 5. Consumer endpoint map

All paths are relative to `/api/v1`.

| Area | Old (Go) | New |
|---|---|---|
| Profile | `GET/PUT /users/me` | `GET/PUT /me`. PUT takes `{fullName, address, dateOfBirth, sex, allergies, language, version}`. Phone and email are identity fields and cannot be changed here. |
| | `PUT/DELETE /users/me/photo` | `PUT/DELETE /me/photo` (multipart `file`). `GET /me/photo` returns a signed URL. |
| | `PUT /users/me/password {new_password}` | `PUT /me/password {currentPassword?, newPassword}` → a fresh token response (other sessions are revoked) |
| | — | `DELETE /me` schedules erasure after 30 days. |
| Reference | `GET /icd10?q=`, `GET /drugs?q=` | Same paths, camelCase fields. `GET /specialties` now exists, so the hard-coded `SPECIALTIES` list can go. |
| Doctors | `GET /doctors?per_page&sort=rating&q&specialty&min_fee&max_fee` | `GET /doctors?q&specialty&language&minFee&maxFee&sort=name\|fee\|experience&page&pageSize`. There is no rating sort (reviews were removed). |
| | `GET /doctors/{id}` | Same path. `specialtyCode`, `feeCents`, `photoUrl`, `qualifications[]` and so on. The fields `rating`, `review_count`, `consultation_count`, `verification_status` and `next_available_at` are gone. |
| Slots | `GET /doctors/{id}/slots?date=` (auth, once per day) → `{slots:[{id, start_at, …, status}]}` | `GET /doctors/{id}/slots?from=YYYY-MM-DD&to=YYYY-MM-DD` (**public**, one call for the whole range, capped by the doctor's advance days) → `{timezone, slots:[{startAt, endAt, available}]}`. Slots have **no id**. |
| Booking | `POST /appointments {slot_id, doctor_id, visit_patient_name, …, intake}` | `POST /appointments {doctorId, startAt, visitPatient:{name, dateOfBirth, sex?, weightKg?, allergies?}, intake:{symptoms, visitRelation?}}` → 201 appointment in `pendingPayment`, with `paymentDueAt` 15 minutes later. Send `startAt` exactly as the slot returned it. A taken time returns `409 slot_unavailable`, and an overlap with another of the patient's bookings returns `409 patient_overlap`. |
| Appointments | `GET /appointments?per_page&status`, `GET /appointments/{id}` | Same paths, paged. Fields: `feeCents`, `visitPatient{…}`, `intake{…}`, `cancelledBy`, `refundPercent`, `isTest`. `slot_id`, `*_local`, `counterpart_name`, `family_member_id` and `specialty` are gone. |
| | `POST /appointments/{id}/complete` | Same. Also `POST /appointments/{id}/cancel {reason?}` (refund per policy) and `POST /appointments/{id}/no-show` (doctor). |
| | `GET /appointments/last-visit-details` → `{weight_kg}` | Same path, returning `{name, dateOfBirth, sex, weightKg, allergies}` |
| Reschedule | `…/reschedule-requests`, `/reschedule-requests/{id}/accept\|decline` | Same paths. Propose with `{proposedStartAt, reason?}`. Times are UTC only. |
| Payment | `GET /payments/order/{appointmentId}` | `GET /appointments/{id}/payment` → `{paymentId, appointmentId, status, grossCents, discountCents, amountCents, currency, promoCode, …}` |
| | — | `PUT /appointments/{id}/payment/promo {code}` and `DELETE` to remove it |
| | `POST /payments/intent {appointment_id, provider, return_url?}` → `{next_action, redirect_url, reference(JSON string)}` | `POST /appointments/{id}/payment/intent {provider: "payhere" \| "mock"}` → `{paymentId, provider, status, authorizeOnly, amountCents, currency, reference, checkout}`. For PayHere, POST a form of `checkout.fields` to `checkout.actionUrl`; the fields are a real object, not a JSON string. Return and cancel URLs are server config. |
| | `GET /payments/{id}` | Same, plus `GET /payments` (paged history). A payment can be `authorized` (card hold) before it is `succeeded`. |
| Consultation | `POST /consultations/{appointmentId}/join` | `POST /appointments/{appointmentId}/consultation/join` → `{consultationId, role, status, roomToken, hubUrl, iceServers[{urls, username?, credential?}], counterpartName, scheduledStartAt, scheduledEndAt}` |
| | `/consultations/{consultationId}`, `/waiting-room`, `/admit`, `/end {reason}`, `/quality`, `/messages`, `/early-join*`, `/ready-for-next` | **All keyed by appointment id now**: `/appointments/{appointmentId}/consultation`, `…/consultation/waiting-room`, `…/admit`, `…/end`, `…/quality {quality, packetLossPct?, bitrateKbps?}`, `…/messages` (`POST {body}`; list is paged), `…/early-join`, `…/early-join/accept\|decline`, `…/ready-for-next`. Polling `GET …/consultation` still works, but `state-changed` hub frames make it unnecessary. |
| Clinical notes | `GET/PUT /clinical-notes/{appointmentId}`, `/finalise`, `/amend` | `GET/PUT /appointments/{appointmentId}/clinical-note {subjective, objective, assessment, plan, diagnoses[{code, isPrimary}], version}`, `POST …/finalise {version}`, `POST …/amend {reason, …sections, version}`, `GET …/revisions`. `GET /clinical-notes` lists the doctor's notes. |
| Prescriptions | `GET /prescriptions?appointment_id=`, `POST /prescriptions {appointment_id, doctor_name, patient_*, items}` | `GET/POST /appointments/{appointmentId}/prescription {items[{drugName, strength, form, dosage, frequency, durationDays, quantity, instructions?, isGeneric}]}`. The doctor and patient details are **snapshotted by the server**, so don't send them. The doctor must have uploaded a signature and seal first (`409 stamps_required`). |
| | `GET /prescriptions/{id}/pdf` | Same path. Always PDF bytes. Also `GET /prescriptions` (paged), `POST /prescriptions/{id}/cancel`, and the public `GET /prescriptions/{id}/verify?h=` that the QR code points at. |
| Medical reports | — | `GET/POST /appointments/{appointmentId}/medical-report`. Body: `{addressee?, clinicalImpression, findings?, advice?, fitness: notAssessed\|fit\|unfit\|restricted, leaveFrom?, leaveUntil?, returnToWorkOn?, fitnessNotes?}`. Same treating-window and stamp rules as prescriptions (`409 medical_report_exists`, `stamps_required`). PDF at `GET /medical-reports/{id}/pdf`; public `GET /medical-reports/{id}/verify?h=`. |
| Vault | `/records…` | `/vault/documents` (`GET` paged with `?folderId&documentType&patientId`, `POST` multipart upload up to 10 MB), `PATCH/DELETE /vault/documents/{id}`, `GET /vault/documents/{id}/download` → `{url}`, a short-lived signed `/api/v1/files/{token}` link, `/vault/folders` (`GET/POST`, `PATCH/DELETE {id}`), `GET /vault/patients`. `/records/{id}/content` is gone: fetch the signed URL instead. |
| Doctor onboarding | `GET /doctors/applications/eligibility?phone=` | `GET /doctor-applications/eligibility?phone=` → `{status: none\|pending\|underReview\|approved\|rejected\|doctor, applicationId?, doctorId?}` |
| | `POST /doctors/apply` → `{application_id}` | `POST /doctor-applications` (camelCase; `feeCents` replaces `required_fee_lkr`/`fee_lkr`; `specialtyCode`) → `{id, uploadToken}` |
| | `POST /doctors/applications/{id}/documents` (multipart plus `document_type`) | `PUT /doctor-applications/{id}/documents/{type}` with header **`X-Upload-Token`** and multipart `file`. `type` is one of `slmcCertificate`, `nic`, `degreeCertificate`, `specialtyBoardCertificate`, `signature`, `seal`, `other`. |
| Doctor self | `GET/PUT /doctors/me`, photo, signature, seal, documents | Same paths (camelCase, `feeCents`). |
| | `GET/PUT /doctors/me/availability` plus `GET /doctors/me/schedule-settings` | **One resource:** `GET/PUT /doctors/me/schedule {slotDurationMinutes, bufferMinutes, maxPerDay, advanceDays, timezone, workingHours[{dayOfWeek 0-6, startMinute, endMinute}]}`. Times are minutes from midnight (0..1440); there are no `start_time` strings. The change is visible in the very next `GET /slots`, and the response adds `appointmentsOutsideNewHours`. |
| | holidays inside the availability PUT; `GET /doctors/me/holidays` | `GET/POST /doctors/me/holidays {date, reason, cancelBooked}` and `DELETE /doctors/me/holidays/{id}`. If booked appointments are affected and `cancelBooked` is false, the response is `409 appointments_affected`. |
| | `/doctors/me/analytics`, `/analytics/peak-hours`, `/earnings` | Same paths, camelCase. Also `GET /doctors/me/payouts`. |

## 6. Admin endpoint map

All paths are under `/api/v1/admin`.

| Old | New |
|---|---|
| `me` | `me`; `permissions` replaces `admin-users/permissions` |
| `doctors/pending`, `doctors/{id}` (applications) | `doctor-applications?status=pending` and `doctor-applications/{id}`. Approved doctors are `doctors` and `doctors/{id}`, with `suspend {reason}` and `reinstate`. |
| `doctors/{id}/checklist` | `PUT doctor-applications/{id}/checklist {slmcFormat?, slmcRegistry?, experience?, nicMatch?, photoClarity?}` |
| `doctors/{id}/verify {action, reason}` | `POST doctor-applications/{id}/start-review`, `POST …/approve`, `POST …/reject {reason}` |
| `doctors/{id}/application?include=bytes` | `GET doctor-applications/{id}/documents/{documentId}` (or `doctors/{id}/documents/{documentId}`) → `{url}`, a signed link |
| `notifications`, `unread-count`, `{id}/read`, `read-all` | Unchanged |
| `users?query=` | `users?q=&role=&status=`; `users/{id}`; `users/{id}/activity` → `{appointments: summary, audit: []}`; `suspend {reason}`; `reinstate` (no body). Suspending a patient now cancels their upcoming bookings with a full refund. Suspending a doctor's account also suspends the doctor profile. |
| `appointments`, `appointments/{id}` | Same, plus `?patientId&doctorId&status&from&to&includeTest`. Detail is `{appointment, payment, rescheduleRequests}`. |
| `appointments/{id}/force-cancel {reason, refund}` | `POST appointments/{id}/cancel {reason}`. The refund follows policy; for any other amount, create a refund (below). |
| `appointments/double-bookings`, `resolve-double-booking` | **Removed.** Database exclusion constraints make double bookings impossible. |
| `appointments/{id}/audit` | Same (plain list) |
| `reschedule-requests`, `{id}/accept\|decline` | Unchanged |
| `finance/ledger`, `finance/ledger/export` | `finance/ledger`, `finance/ledger.csv` |
| `finance/commission-rules` (GET/PUT/history) | `GET/PUT finance/commission` (default rate in basis points). `PUT finance/commission/doctors/{doctorId} {commissionBps}` sets or clears (`null`) a doctor override. Doctors without an override use the default. Provider fee and payout hold stay code constants. |
| `finance/payout-batches`, `finance/payouts/run {from, to}` | `finance/payout-batches`, `finance/payout-batches/{id}`, `POST finance/payouts/run {date?}` (one closed day; the default is yesterday), `POST finance/payouts/{id}/mark-paid {transferReference}`, `POST finance/payouts/{id}/mark-failed {reason}` |
| `finance/refunds/{id}/decision {decision, note}` | `POST finance/refunds/{id}/approve`, `…/reject {reason}`, `…/mark-refunded {reference}` (for manual_required refunds). A new manual refund is `POST payments/{paymentId}/refunds {amountCents, reason}`. |
| `finance/promo-codes?include_inactive`, `POST`, `DELETE {code}` | `finance/promo-codes?isActive=`, `POST`, `PATCH {id}`, `POST {id}/deactivate` |
| content `specialties`, `drugs` | `specialties` (keyed by `code`) and `drugs`, with GET/POST/PUT/DELETE |
| content `symptoms`, `articles` | **Removed** |
| `disputes…` | `disputes` (`POST {appointmentId, subject, description}`), `{id}`, `{id}/comments {body}`, `{id}/assign {adminUserId?}`, `{id}/resolve {resolution, refundAmountCents?}`, `{id}/close` |
| `configs/{key}` (+history) | **Removed.** Platform policy values are constants. |
| `admin-users` GET/POST, `PUT {id} {role\|active, version}` | `admin-users` GET/POST `{email, displayName, role}`, `PATCH {id} {displayName?, role?, isActive?}`, `POST {id}/deactivate` |
| `doctors/{id}/availability`, `doctors/{id}/schedule-settings` | `GET/PUT doctors/{id}/schedule` (same body as the doctor's own) |
| `POST holidays {doctor_id?, …, cancel_booked}` | `GET/POST doctors/{id}/holidays` for one doctor; `GET/POST holidays` for platform-wide; `DELETE holidays/{id}`. Body: `{date, reason, cancelBooked}`. |
| `POST slots/{slotId}/block` | `GET/POST doctors/{id}/slot-blocks {startAt, endAt, reason, cancelBooked}`, `DELETE slot-blocks/{id}` |
| `analytics/dashboard\|revenue\|bookings\|doctors` | `analytics/dashboard\|revenue\|bookings\|top-doctors`. `utilization` and `districts` were removed. |
| `audit`, `audit/export`, `audit/verify` | `audit`, `audit.csv`. `verify` was **removed** because there is no hash chain. |

## 7. Realtime: SignalR replaces the raw WebSocket

| | Old | New |
|---|---|---|
| Endpoint | `wss://…{signal_url}?token=` (raw WebSocket) | The SignalR hub at `hubUrl` from the join response (`/hubs/consultation`), with `?roomToken=<roomToken>` |
| Client | Hand-rolled socket, ping every 20s, own reconnect | `@microsoft/signalr` `HubConnection`. Its keep-alive and `withAutomaticReconnect()` replace the ping loop. The API uses the JSON protocol, and all transports are allowed. |
| Auth | join `token` | join `roomToken`, valid for about 10 minutes. Call join again to get a fresh one before reconnecting after a long drop. |
| Send | `ws.send(JSON.stringify(frame))` | `connection.invoke("Send", {type, data})` |
| Receive | `onmessage` | `connection.on("frame", frame => …)` |

- **Frame shape:** `{type, from?, data?}`, the same shape as before. The hub stamps `from` with the sender's connection id and ignores any `from` the client sends.
- **Client → peer** (relayed untouched): `offer`, `answer`, `ice`, `bye`, `chat` (typing or ephemeral), `pointer`, `file_shared`, `quality`. `ping` gets a `pong` back. Any other type returns an `error` frame with `UNKNOWN_TYPE`. **`recording-state` is no longer relayed** (recording was removed).
- **Server → client:**
  - `welcome` `{data: {peerId, role, polite, peerPresent, iceServers}}`. The field names changed from `peer_id`, `peer_present` and `ice_servers`, and there is no `room`.
  - `peer-joined` and `peer-left` (`from` = peer id).
  - `room-closed` `{data: {reason}}`. `reason` is `replaced` when the same user connects again elsewhere, or a problem `code` such as `consultation_ended`.
  - `state-changed` `{data: <consultation>}`: waiting, admitted, started or ended. This replaces polling.
  - `chat` `{data: <message>}` for persisted messages sent with `POST …/consultation/messages`.
  - `error` `{data: {code: UNKNOWN_TYPE | MALFORMED | NO_PEER, message}}`.
- **Room full:** `ROOM_FULL` is gone. A room has one patient and one doctor, and a second connection for the same role replaces the first.
- **ICE:** use `iceServers` from `welcome` or join. They are Cloudflare TURN when configured, and STUN otherwise.

## 8. Removed features

There are no endpoints for any of the following. Remove them from the UI.

- Waitlist, overbooking and no-show prepayment.
- Stripe, saved cards and Dialog PIN payments. **PayHere and Mock remain.**
- Family members (`family_member_id`). Use `visitPatient` and `intake.visitRelation` instead.
- Reviews and ratings (`rating`, `review_count`, `sort=rating`).
- Record shares, the consent ledger, notification preferences, devices and push.
- LiveKit, recording and FHIR. `provider`, `livekit_url` and `recording_mode` are gone from join.
- The admin config UI and feature flags (`configs/*`). Commission is editable: default platform rate plus optional per-doctor overrides.
- Symptoms and articles CMS.
- Double-booking tools and audit chain verification.
- The Go test mode (`/test/status`, `/test/outbox`, `/test/rooms`). It is replaced by per-feature switches:
  - `GET /test/captured-messages[?recipient&channel]`, `GET /test/captured-messages/latest?recipient=` and `DELETE /test/captured-messages` read the SMS and email that the Capture senders stored.
  - `POST /test/instant-meetings {counterpartUserId | phone | email}` creates an `isTest` appointment that starts now, needs no payment, and can be joined at once.
  - Both need the `X-Test-Secret` header and return 404 when their switch is off. Instant meetings also need the caller's bearer token.
