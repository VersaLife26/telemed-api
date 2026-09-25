using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Video;

internal sealed class AppLinks(IOptions<AppLinksOptions> options) : IAppLinks
{
    public string ConsultationJoin(Guid appointmentId) => $"{Base}/appointments/{appointmentId}/consultation";

    public string Prescription(Guid prescriptionId) => $"{Base}/prescriptions/{prescriptionId}";

    private string Base => options.Value.PatientAppUrl.TrimEnd('/');
}
