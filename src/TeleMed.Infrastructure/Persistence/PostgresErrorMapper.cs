using Microsoft.EntityFrameworkCore;
using Npgsql;
using TeleMed.Application.Common.Exceptions;
using TeleMed.Infrastructure.Persistence.Configurations;

namespace TeleMed.Infrastructure.Persistence;

internal static class PostgresErrorMapper
{
    private static readonly Dictionary<string, (string Code, string Message)> KnownConstraints = new()
    {
        ["ux_users_normalized_email"] = ("email_taken", "An account with this email already exists."),
        ["ux_users_phone_number"] = ("phone_taken", "An account with this phone number already exists."),
        ["ux_admin_users_email"] = ("admin_email_taken", "An admin user with this email already exists."),
        ["pk_specialties"] = ("specialty_exists", "A specialty with this code already exists."),
        ["ux_doctor_applications_phone_open"] = ("application_exists", "An open application already exists for this phone number."),
        ["ux_doctor_applications_slmc_number_open"] = ("application_exists", "An open application already exists for this SLMC number."),
        ["ux_doctors_slmc_number"] = ("slmc_registered", "A doctor with this SLMC number is already registered."),
        ["ux_doctors_user_id"] = ("already_doctor", "This account already has a doctor profile."),
        ["ux_doctor_documents_doctor_stamp_live"] = ("concurrent_upload", "Another upload replaced this document at the same time. Try again."),
        ["ux_doctor_documents_application_type_live"] = ("concurrent_upload", "Another upload replaced this document at the same time. Try again."),
        ["ux_holidays_doctor_date"] = ("holiday_exists", "A holiday already exists on this date."),
        [AppointmentConfiguration.DoctorOverlapConstraint] = ("slot_unavailable", "This time is no longer available."),
        [AppointmentConfiguration.PatientOverlapConstraint] = ("patient_overlap", "You already have an appointment at this time."),
        ["ux_payments_appointment_id"] = ("payment_exists", "This appointment already has a payment."),
        ["ux_promo_redemptions_payment_live"] = ("promo_conflict", "A promo code was applied to this payment at the same time. Try again."),
        [RescheduleRequestConfiguration.PendingPerAppointmentIndex] = ("reschedule_pending", "This appointment already has a pending reschedule request."),
        ["ux_payment_webhook_events_provider_event_id"] = ("webhook_duplicate", "This notification was already processed."),
        [ClinicalNoteConfiguration.AppointmentIndex] = ("note_exists", "A note was started for this appointment at the same time. Reload and try again."),
        [PrescriptionConfiguration.AppointmentIndex] = ("prescription_exists", "A prescription has already been issued for this appointment."),
        ["ux_promo_codes_code"] = ("promo_code_exists", "A promo code with this code already exists."),
        ["ux_payout_batches_period_end"] = ("payout_run_in_progress", "Payouts for this day were built at the same time. Reload the batch."),
        ["ux_payouts_doctor_id_period"] = ("payout_run_in_progress", "Payouts for this day were built at the same time. Reload the batch."),
        [VaultFolderConfiguration.SiblingNameIndex] = ("folder_exists", "A folder with this name already exists here."),
    };

    public static ConflictException? Map(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return new ConflictException("concurrency_conflict", "The record was changed by someone else. Reload and try again.", exception);
        }

        if (exception.InnerException is not PostgresException pg)
        {
            return null;
        }

        var generic = pg.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => ("duplicate", "A record with the same value already exists."),
            PostgresErrorCodes.ExclusionViolation => ("overlap", "This overlaps an existing record."),
            PostgresErrorCodes.ForeignKeyViolation => ("in_use", "This entry is referenced elsewhere and cannot be deleted."),
            _ => ((string, string)?)null,
        };
        if (generic is null)
        {
            return null;
        }

        var (code, message) = pg.ConstraintName is { } name && KnownConstraints.TryGetValue(name, out var known)
            ? known
            : generic.Value;
        return new ConflictException(code, message, exception);
    }
}
