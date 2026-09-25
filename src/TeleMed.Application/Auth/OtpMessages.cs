using TeleMed.Domain.Enums;

namespace TeleMed.Application.Auth;

internal static class OtpMessages
{
    public static string Subject(Language language) => language switch
    {
        Language.Si => "VersaLife තහවුරු කිරීමේ කේතය",
        Language.Ta => "VersaLife சரிபார்ப்புக் குறியீடு",
        _ => "Your VersaLife verification code",
    };

    public static string Body(Language language, string code) => language switch
    {
        Language.Si => $"ඔබගේ VersaLife කේතය {code}. විනාඩි 5ක් වලංගුයි.",
        Language.Ta => $"உங்கள் VersaLife குறியீடு {code}. 5 நிமிடங்கள் செல்லுபடியாகும்.",
        _ => $"Your VersaLife code is {code}. Valid for 5 minutes.",
    };
}
