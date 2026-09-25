// End-to-end smoke flow from the plan's Verification section, run against a live stack (see scripts/smoke.sh).
// Node 22+ only: uses the built-in fetch and WebSocket. Reads the prescription HMAC through `docker compose exec`.
import { createHmac, randomInt } from "node:crypto";
import { execFileSync } from "node:child_process";

const BASE = (process.env.SMOKE_BASE_URL ?? "http://127.0.0.1:8080").replace(/\/$/, "");
const API = `${BASE}/api/v1`;
const TEST_SECRET = process.env.SMOKE_TEST_SECRET ?? "dev-only-test-secret";
const ADMIN_KEY = process.env.SMOKE_ADMIN_KEY ?? "dev-only-admin-jwt-signing-key-change-me-0123";
const ADMIN_EMAIL = process.env.SMOKE_ADMIN_EMAIL ?? "admin@telemed.local";
const ADMIN_ORIGIN = process.env.SMOKE_ADMIN_ORIGIN ?? "http://localhost:3001";
const PERF_RUNS = Number(process.env.SMOKE_PERF_RUNS ?? 50);

const PNG = Buffer.from(
  "89504E470D0A1A0A0000000D4948445200000001000000010806000000" +
    "1F15C4890000000D49444154789C6360606060F80F0001040100" +
    "5FE5C34B0000000049454E44AE426082",
  "hex",
);

const results = [];
const ctx = {};

async function step(name, fn) {
  const started = performance.now();
  try {
    const detail = await fn();
    results.push({ name, ok: true, ms: performance.now() - started, detail });
    console.log(`PASS  ${name}${detail ? `  (${detail})` : ""}`);
  } catch (err) {
    results.push({ name, ok: false, ms: performance.now() - started, detail: err.message });
    console.log(`FAIL  ${name}: ${err.message}`);
    throw err;
  }
}

function check(condition, message) {
  if (!condition) throw new Error(message);
}

async function call(method, path, { token, body, headers = {}, form, expect } = {}) {
  const init = { method, headers: { ...headers } };
  if (token) init.headers.Authorization = `Bearer ${token}`;
  if (form) init.body = form;
  else if (body !== undefined) {
    init.headers["Content-Type"] = "application/json";
    init.body = JSON.stringify(body);
  }
  const response = await fetch(path.startsWith("http") ? path : `${API}${path}`, init);
  const bytes = Buffer.from(await response.arrayBuffer());
  const type = response.headers.get("content-type") ?? "";
  const json = type.includes("json") && bytes.length ? JSON.parse(bytes.toString("utf8")) : undefined;
  if (expect !== undefined && response.status !== expect) {
    throw new Error(`${method} ${path} -> ${response.status}, expected ${expect}: ${bytes.toString("utf8").slice(0, 300)}`);
  }
  return { status: response.status, json, bytes, type };
}

function adminToken() {
  const b64 = (o) => Buffer.from(JSON.stringify(o)).toString("base64url");
  const now = Math.floor(Date.now() / 1000);
  const unsigned = `${b64({ alg: "HS256", typ: "JWT" })}.${b64({
    iss: "telemed-admin-local",
    aud: "telemed-admin",
    email: ADMIN_EMAIL,
    iat: now,
    nbf: now,
    exp: now + 3600,
  })}`;
  return `${unsigned}.${createHmac("sha256", ADMIN_KEY).update(unsigned).digest("base64url")}`;
}

function colomboDate(offsetDays = 0) {
  const d = new Date(Date.now() + offsetDays * 86_400_000);
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Colombo" }).format(d);
}

function psql(sql) {
  return execFileSync("docker", ["compose", "exec", "-T", "postgres", "psql", "-U", "telemed", "-d", "telemed", "-tAc", sql], {
    encoding: "utf8",
  }).trim();
}

function file(bytes, name, type = "image/png") {
  const form = new FormData();
  form.append("file", new Blob([bytes], { type }), name);
  return form;
}

// Minimal SignalR JSON-protocol client over a raw WebSocket (skip-negotiation mode).
class HubPeer {
  static RS = "\u001e";

  constructor(url) {
    this.frames = [];
    this.waiters = [];
    this.ws = new WebSocket(url);
    this.ready = new Promise((resolve, reject) => {
      this.ws.addEventListener("open", () => this.ws.send(JSON.stringify({ protocol: "json", version: 1 }) + HubPeer.RS));
      this.ws.addEventListener("error", () => reject(new Error("websocket error")));
      this.ws.addEventListener("message", (event) => {
        for (const raw of String(event.data).split(HubPeer.RS).filter(Boolean)) {
          const message = JSON.parse(raw);
          if (message.error) reject(new Error(`hub handshake: ${message.error}`));
          else if (message.type === undefined) resolve();
          else if (message.type === 1 && message.target === "frame") this.push(message.arguments[0]);
          else if (message.type === 7) this.push({ type: "__closed", data: message.error });
        }
      });
    });
  }

  push(frame) {
    this.frames.push(frame);
    for (const waiter of [...this.waiters]) waiter();
  }

  send(frame) {
    this.ws.send(JSON.stringify({ type: 1, target: "Send", arguments: [frame] }) + HubPeer.RS);
  }

  next(type, timeoutMs = 10_000) {
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error(`timed out waiting for '${type}' frame`)), timeoutMs);
      const look = () => {
        const index = this.frames.findIndex((f) => f.type === type);
        if (index < 0) return;
        clearTimeout(timer);
        this.waiters.splice(this.waiters.indexOf(look), 1);
        resolve(this.frames.splice(index, 1)[0]);
      };
      this.waiters.push(look);
      look();
    });
  }

  close() {
    this.ws.close();
  }
}

const unique = randomInt(1_000_000, 9_999_999);
const patientPhone = `+9470${unique}`;
const doctor = {
  phone: `+9477${unique}`,
  email: `smoke.doctor.${unique}@example.com`,
  password: "smoke doctor password",
  slmc: String(unique).slice(0, 6),
};

async function main() {
  await step("0. GET /health/ready returns 200", async () => {
    await call("GET", `${BASE}/health/ready`, { expect: 200 });
  });

  await step("1. Register a patient via OTP read from the capture inbox", async () => {
    await call("POST", "/auth/otp/send", { body: { phone: patientPhone }, expect: 200 });
    const latest = await call("GET", `/test/captured-messages/latest?recipient=${encodeURIComponent(patientPhone)}`, {
      headers: { "X-Test-Secret": TEST_SECRET },
      expect: 200,
    });
    const code = latest.json.body.match(/\b(\d{6})\b/)?.[1];
    check(code, `no 6-digit code in captured message: ${latest.json.body}`);
    const auth = await call("POST", "/auth/otp/verify", { body: { phone: patientPhone, code }, expect: 200 });
    check(auth.json.user.role === "patient", `role ${auth.json.user.role}`);
    ctx.patient = { token: auth.json.accessToken, id: auth.json.user.id };
    return `patient ${ctx.patient.id}`;
  });

  await step("2. Apply as a doctor and approve via the local admin JWT", async () => {
    const applied = await call("POST", "/doctor-applications", {
      expect: 201,
      body: {
        phone: doctor.phone,
        email: doctor.email,
        password: doctor.password,
        firstName: "Smoke",
        lastName: "Doctor",
        slmcNumber: doctor.slmc,
        specialtyCode: "general_practice",
        languages: ["en", "si"],
        experienceYears: 8,
        feeCents: 250_000,
        bio: "Smoke-test physician.",
        pgimBoardCertified: false,
        isGeneralPractitioner: true,
        medicalSchool: "University of Colombo",
        qualificationsText: "MBBS (Colombo)",
        availabilityNotes: "Any time",
        practicingLocations: ["Colombo 07"],
        termsAccepted: true,
        bank: { bankName: "Bank of Ceylon", branchName: "Colombo Fort", accountNumber: "0012345678", accountName: "S. Doctor" },
      },
    });
    const approved = await call("POST", `/admin/doctor-applications/${applied.json.id}/approve`, {
      token: adminToken(),
      headers: { Origin: ADMIN_ORIGIN },
      expect: 200,
    });
    check(approved.json.status === "approved" && approved.json.doctorId, `application ${approved.json.status}`);
    const login = await call("POST", "/auth/login/email", { body: { email: doctor.email, password: doctor.password }, expect: 200 });
    ctx.doctor = { token: login.json.accessToken, userId: login.json.user.id, id: approved.json.doctorId };
    return `doctor ${ctx.doctor.id}`;
  });

  await step("3. Doctor sets working hours and slots appear immediately", async () => {
    const slotsUrl = `/doctors/${ctx.doctor.id}/slots?from=${colomboDate()}&to=${colomboDate(1)}`;
    const before = await call("GET", slotsUrl, { expect: 200 });
    check(before.json.slots.length === 0, `expected no slots before hours are set, got ${before.json.slots.length}`);
    await call("PUT", "/doctors/me/schedule", {
      token: ctx.doctor.token,
      expect: 200,
      body: {
        slotDurationMinutes: 15,
        bufferMinutes: 0,
        maxPerDay: 96,
        advanceDays: 30,
        timezone: "Asia/Colombo",
        workingHours: [0, 1, 2, 3, 4, 5, 6].map((dayOfWeek) => ({ dayOfWeek, startMinute: 0, endMinute: 1440 })),
      },
    });
    const after = await call("GET", slotsUrl, { expect: 200 });
    const free = after.json.slots.filter((s) => s.available);
    check(free.length > 0, "no available slots right after the schedule write");
    ctx.slot = free[0];
    return `${free.length} free slots, first ${ctx.slot.startAt}`;
  });

  await step("4. Book the slot and pay with the mock provider", async () => {
    const booked = await call("POST", "/appointments", {
      token: ctx.patient.token,
      expect: 201,
      body: {
        doctorId: ctx.doctor.id,
        startAt: ctx.slot.startAt,
        visitPatient: { name: "Kamal Silva", dateOfBirth: "1990-04-01", sex: "male", weightKg: 70.5, allergies: "Penicillin" },
        intake: { symptoms: "Fever for two days" },
      },
    });
    check(booked.json.status === "pendingPayment", `status ${booked.json.status}`);
    const intent = await call("POST", `/appointments/${booked.json.id}/payment/intent`, {
      token: ctx.patient.token,
      body: { provider: "mock" },
      expect: 200,
    });
    await call("POST", `/payments/${intent.json.paymentId}/mock/complete`, { token: ctx.patient.token, body: { outcome: "succeed" }, expect: 200 });
    const confirmed = await call("GET", `/appointments/${booked.json.id}`, { token: ctx.patient.token, expect: 200 });
    check(confirmed.json.status === "confirmed", `status ${confirmed.json.status}`);
    ctx.appointmentId = booked.json.id;
    return `appointment ${ctx.appointmentId} confirmed`;
  });

  await step("5. Instant meeting joined from two SignalR clients (offer/answer relayed)", async () => {
    const meeting = await call("POST", "/test/instant-meetings", {
      token: ctx.doctor.token,
      headers: { "X-Test-Secret": TEST_SECRET },
      body: { counterpartUserId: ctx.patient.id },
      expect: 201,
    });
    const id = meeting.json.appointmentId;
    const patientJoin = await call("POST", `/appointments/${id}/consultation/join`, { token: ctx.patient.token, expect: 200 });
    const doctorJoin = await call("POST", `/appointments/${id}/consultation/join`, { token: ctx.doctor.token, expect: 200 });
    const hub = (join) => `${BASE.replace(/^http/, "ws")}${new URL(join.json.hubUrl, BASE).pathname}?roomToken=${encodeURIComponent(join.json.roomToken)}`;

    const patientPeer = new HubPeer(hub(patientJoin));
    await patientPeer.ready;
    const welcome = await patientPeer.next("welcome");
    check(welcome.data.role === "patient" && welcome.data.peerPresent === false, JSON.stringify(welcome.data));
    const doctorPeer = new HubPeer(hub(doctorJoin));
    await doctorPeer.ready;
    const doctorWelcome = await doctorPeer.next("welcome");
    check(doctorWelcome.data.peerPresent === true, "doctor did not see the patient present");
    await patientPeer.next("peer-joined");

    doctorPeer.send({ type: "offer", data: { sdp: "v=0 smoke-offer" } });
    const offer = await patientPeer.next("offer");
    check(offer.data.sdp === "v=0 smoke-offer" && offer.from === doctorWelcome.data.peerId, JSON.stringify(offer));
    patientPeer.send({ type: "answer", data: { sdp: "v=0 smoke-answer" } });
    const answer = await doctorPeer.next("answer");
    check(answer.data.sdp === "v=0 smoke-answer", JSON.stringify(answer));

    const third = new HubPeer(hub(patientJoin));
    await third.ready;
    await third.next("welcome");
    await patientPeer.next("room-closed");
    patientPeer.close();
    doctorPeer.close();
    third.close();
    return `consultation ${patientJoin.json.consultationId}; a reconnect replaced the old patient connection`;
  });

  await step("6. Finalise a note, issue a prescription, fetch its PDF and verify it", async () => {
    const id = ctx.appointmentId;
    await call("POST", `/appointments/${id}/consultation/join`, { token: ctx.patient.token, expect: 200 });
    await call("POST", `/appointments/${id}/consultation/join`, { token: ctx.doctor.token, expect: 200 });
    await call("POST", `/appointments/${id}/consultation/admit`, { token: ctx.doctor.token, expect: 200 });

    const draft = await call("PUT", `/appointments/${id}/clinical-note`, {
      token: ctx.doctor.token,
      expect: 200,
      body: { subjective: "Fever for two days", assessment: "Viral fever", plan: "Rest and fluids", diagnoses: [{ code: "R50.9", isPrimary: true }] },
    });
    const note = await call("POST", `/appointments/${id}/clinical-note/finalise`, { token: ctx.doctor.token, body: { version: draft.json.version }, expect: 200 });
    check(note.json.status === "finalised", `note ${note.json.status}`);

    await call("PUT", "/doctors/me/signature", { token: ctx.doctor.token, form: file(PNG, "signature.png"), expect: 200 });
    await call("PUT", "/doctors/me/seal", { token: ctx.doctor.token, form: file(PNG, "seal.png"), expect: 200 });
    const rx = await call("POST", `/appointments/${id}/prescription`, {
      token: ctx.doctor.token,
      expect: 201,
      body: {
        items: [
          { drugName: "Paracetamol", strength: "500mg", form: "tablet", dosage: "1 tablet", frequency: "3x daily", durationDays: 3, quantity: 9, isGeneric: true },
        ],
      },
    });

    const pdf = await call("GET", `/prescriptions/${rx.json.id}/pdf`, { token: ctx.patient.token, expect: 200 });
    check(pdf.type.startsWith("application/pdf") && pdf.bytes.subarray(0, 5).toString() === "%PDF-", `not a PDF (${pdf.type})`);

    const hmac = psql(`SELECT verification_hmac FROM prescriptions WHERE id = '${rx.json.id}'`);
    const verified = await call("GET", `/prescriptions/${rx.json.id}/verify?h=${encodeURIComponent(hmac)}`, { expect: 200 });
    check(verified.json.valid === true, JSON.stringify(verified.json));
    const flipped = (hmac[0] === "0" ? "1" : "0") + hmac.slice(1);
    const tampered = await call("GET", `/prescriptions/${rx.json.id}/verify?h=${encodeURIComponent(flipped)}`, { expect: 200 });
    check(tampered.json.valid === false, "tampered HMAC verified");
    return `prescription ${rx.json.id}, PDF ${pdf.bytes.length} bytes rendered in the container, verify valid`;
  });

  await step(`7. Perf: slots for one doctor over 31 days (${PERF_RUNS} runs)`, async () => {
    const url = `/doctors/${ctx.doctor.id}/slots?from=${colomboDate()}&to=${colomboDate(30)}`;
    let count = 0;
    for (let i = 0; i < 5; i++) count = (await call("GET", url, { expect: 200 })).json.slots.length;
    const samples = [];
    for (let i = 0; i < PERF_RUNS; i++) {
      const t = performance.now();
      await call("GET", url, { expect: 200 });
      samples.push(performance.now() - t);
    }
    samples.sort((a, b) => a - b);
    const pct = (p) => samples[Math.min(samples.length - 1, Math.floor((p / 100) * samples.length))].toFixed(1);
    ctx.perf = { slots: count, p50: pct(50), p95: pct(95), max: samples.at(-1).toFixed(1) };
    check(Number(ctx.perf.p50) < 50, `p50 ${ctx.perf.p50} ms is over 50 ms`);
    return `${count} slots per response; round trip p50 ${ctx.perf.p50} ms, p95 ${ctx.perf.p95} ms, max ${ctx.perf.max} ms`;
  });
}

try {
  await main();
} catch {
  process.exitCode = 1;
} finally {
  const passed = results.filter((r) => r.ok).length;
  console.log(`\n${passed}/${results.length} steps passed${process.exitCode ? " (stopped at first failure)" : ""}`);
}
