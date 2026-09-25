namespace TeleMed.Application.Abstractions;

public interface IAppLinks
{
    string ConsultationJoin(Guid appointmentId);
    string Prescription(Guid prescriptionId);
}
