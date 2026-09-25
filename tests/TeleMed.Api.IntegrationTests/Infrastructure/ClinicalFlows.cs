using System.Net;
using TeleMed.Application.Appointments;
using static TeleMed.Api.IntegrationTests.Infrastructure.BookingFlows;

namespace TeleMed.Api.IntegrationTests.Infrastructure;

public static class ClinicalFlows
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public sealed record Visit(BookableDoctor Doctor, Patient Patient, Guid AppointmentId);

    public static string NoteUrl(Guid appointmentId, string path = "") => $"/api/v1/appointments/{appointmentId}/clinical-note{path}";

    // An instant test meeting the doctor has admitted the patient to, so the consultation has started.
    public static async Task<Visit> StartedInstantMeetingAsync(this TeleMedApiFactory factory, DoctorFlows.ApplicationSpec? spec = null)
    {
        var doctor = await factory.BookableDoctorAsync(spec);
        var patient = await factory.PatientAsync();
        var id = await patient.Client.InstantMeetingAsync(new { counterpartUserId = doctor.UserId });
        await patient.Client.JoinedAsync(id);
        await doctor.Client.JoinedAsync(id);
        await doctor.Client.AdmittedAsync(id);
        return new Visit(doctor, patient, id);
    }

    // A paid, non-test appointment whose consultation has started; the clock is moved to its start and the clients re-issued.
    public static async Task<Visit> StartedPaidVisitAsync(this TeleMedApiFactory factory)
    {
        var doctor = await factory.BookableDoctorAsync();
        var patient = await factory.PatientAsync();
        AppointmentDto booked = await patient.PaidAsync(doctor.DoctorId, await factory.FreeSlotAsync(doctor.DoctorId, TimeSpan.FromHours(1)));
        factory.Time.Advance(booked.StartAt - factory.Time.GetUtcNow());
        doctor = doctor with { Client = await factory.ClientForAsync(doctor.UserId) };
        patient = patient with { Client = await factory.ClientForAsync(patient.UserId) };
        await patient.Client.JoinedAsync(booked.Id);
        await doctor.Client.JoinedAsync(booked.Id);
        await doctor.Client.AdmittedAsync(booked.Id);
        return new Visit(doctor, patient, booked.Id);
    }

    public static async Task UploadStampsAsync(this HttpClient doctor)
    {
        (await doctor.PutAsync("/api/v1/doctors/me/signature", DoctorFlows.File(DoctorFlows.Png, "signature.png"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await doctor.PutAsync("/api/v1/doctors/me/seal", DoctorFlows.File(DoctorFlows.Png, "seal.png"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public static object PrescriptionBody() => new
    {
        items = new object[]
        {
            new { drugName = "Amoxicillin", strength = "500mg", form = "capsule", dosage = "1 capsule", frequency = "3x daily", durationDays = 7, quantity = 21, isGeneric = true },
            new { drugName = "Paracetamol", strength = "500mg", form = "tablet", dosage = "1 tablet", frequency = "as needed", durationDays = 5, quantity = 10, instructions = "After meals", isGeneric = false },
        },
    };

    public static Task<long> AccessLogCountAsync(this ApiFixture fixture, string where) =>
        fixture.ScalarAsync<long>($"SELECT count(*) FROM record_access_logs WHERE {where}");
}
