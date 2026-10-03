using TeleMed.Domain.Enums;

namespace TeleMed.Application.Abstractions;

public interface IAppLinks
{
    string ConsultationJoin(Guid appointmentId);
    string Prescription(Guid prescriptionId);
    string MedicalReport(Guid medicalReportId);
    string PasswordReset(UserRole role, string token);
}
