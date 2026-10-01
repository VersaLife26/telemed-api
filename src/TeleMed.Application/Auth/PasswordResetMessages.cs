using TeleMed.Domain.Enums;

namespace TeleMed.Application.Auth;

internal static class PasswordResetMessages
{
    public static string Subject(Language language) => language switch
    {
        Language.Si => "VersaLife මුරපදය යළි සැකසීම",
        Language.Ta => "VersaLife கடவுச்சொல்லை மீட்டமைக்கவும்",
        _ => "Reset your VersaLife password",
    };

    public static string Body(Language language, string link, int minutes) => language switch
    {
        Language.Si => $"මුරපදය යළි සැකසීමට මෙම සබැඳිය භාවිතා කරන්න. එය විනාඩි {minutes}ක් වලංගුයි.\n\n{link}\n\nඔබ මෙය ඉල්ලුවේ නැත්නම් මෙම පණිවිඩය නොසලකන්න.",
        Language.Ta => $"உங்கள் கடவுச்சொல்லை மீட்டமைக்க இந்த இணைப்பைப் பயன்படுத்தவும். இது {minutes} நிமிடங்கள் செல்லுபடியாகும்.\n\n{link}\n\nநீங்கள் இதைக் கோரவில்லை என்றால் இந்தச் செய்தியைப் புறக்கணிக்கவும்.",
        _ => $"Use this link to choose a new password. It expires in {minutes} minutes.\n\n{link}\n\nIf you did not ask for this, you can ignore this email.",
    };
}
