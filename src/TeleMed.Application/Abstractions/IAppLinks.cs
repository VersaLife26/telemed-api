using TeleMed.Domain.Enums;

namespace TeleMed.Application.Abstractions;

public interface IAppLinks
{
    string ConsultationJoin(Guid appointmentId);
    string Prescription(Guid prescriptionId);
    string PasswordReset(UserRole role, string token);
}
