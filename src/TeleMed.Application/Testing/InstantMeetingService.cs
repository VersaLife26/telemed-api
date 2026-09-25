using TeleMed.Application.Abstractions;
using TeleMed.Application.Appointments;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Application.Consultations;
using TeleMed.Application.Doctors;
using TeleMed.Domain.Entities;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Application.Testing;

// A real, confirmed consultation starting now, with no slot planning, calendar lock or payment. is_test keeps it out of
// the overlap constraints, availability, reminders, notifications and money flows.
public sealed class InstantMeetingService(
    ICurrentActor actor,
    IUserAccounts users,
    IDoctorRepository doctors,
    IAppointmentRepository appointments,
    IConsultationRepository consultations,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public async Task<InstantMeetingDto> CreateAsync(CreateInstantMeetingRequest request, CancellationToken ct)
    {
        var callerId = actor.RequireUserId();
        var callerRole = actor.Role ?? throw new UnauthorizedException("unauthenticated", "Sign in as a patient or doctor to continue.");
        var counterpart = await FindCounterpartAsync(request, ct);
        if (counterpart is not { Status: UserStatus.Active } || counterpart.Id == callerId)
        {
            throw new NotFoundException("Counterpart not found.");
        }

        if (counterpart.Role == callerRole)
        {
            throw new ConflictException("same_role", "The counterpart must be a doctor when you are a patient, and a patient when you are a doctor.");
        }

        var patient = callerRole == UserRole.Patient ? await users.FindByIdAsync(callerId, ct) ?? throw new NotFoundException("User not found.") : counterpart;
        var doctorUserId = callerRole == UserRole.Doctor ? callerId : counterpart.Id;
        if (await doctors.FindByUserIdAsync(doctorUserId, ct) is not { Status: DoctorStatus.Active } doctor)
        {
            throw new ConflictException("doctor_not_active", "The doctor is not active.");
        }

        var now = time.GetUtcNow();
        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            StartAt = now,
            EndAt = now + TimeSpan.FromMinutes(doctor.SlotDurationMinutes),
            Status = AppointmentStatus.Confirmed,
            ConfirmedAt = now,
            IsTest = true,
            FeeCents = 0,
            Currency = doctor.Currency,
            Intake = new AppointmentIntake("Instant test meeting", null),
            VisitPatientName = string.IsNullOrWhiteSpace(patient.FullName) ? "Test patient" : patient.FullName,
            VisitPatientDateOfBirth = patient.DateOfBirth ?? DateOnly.FromDateTime(now.UtcDateTime),
            VisitPatientSex = patient.Sex,
            VisitPatientAllergies = patient.Allergies,
        };
        appointments.Add(appointment);
        consultations.Add(new Consultation { AppointmentId = appointment.Id });
        await unitOfWork.SaveChangesAsync(ct);
        return new InstantMeetingDto(appointment.Id);
    }

    private async Task<User?> FindCounterpartAsync(CreateInstantMeetingRequest request, CancellationToken ct)
    {
        if (request.CounterpartUserId is { } id)
        {
            return await users.FindByIdAsync(id, ct);
        }

        if (!string.IsNullOrWhiteSpace(request.CounterpartPhone))
        {
            return PhoneNumber.NormalizeSriLankanMobile(request.CounterpartPhone) is { } phone ? await users.FindByPhoneAsync(phone, ct) : null;
        }

        return await users.FindByEmailAsync(request.CounterpartEmail!.Trim(), ct);
    }
}
