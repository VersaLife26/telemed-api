using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Enums;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Video;

internal sealed class AppLinks(IOptions<AppLinksOptions> options) : IAppLinks
{
    public string ConsultationJoin(Guid appointmentId) => $"{Base}/appointments/{appointmentId}/consultation";

    public string Prescription(Guid prescriptionId) => $"{Base}/prescriptions/{prescriptionId}";

    public string PasswordReset(UserRole role, string token)
    {
        var origin = role == UserRole.Doctor && options.Value.DoctorAppUrl.Length > 0
            ? options.Value.DoctorAppUrl
            : options.Value.PatientAppUrl;
        return $"{origin.TrimEnd('/')}/login/reset?token={Uri.EscapeDataString(token)}";
    }

    private string Base => options.Value.PatientAppUrl.TrimEnd('/');
}
