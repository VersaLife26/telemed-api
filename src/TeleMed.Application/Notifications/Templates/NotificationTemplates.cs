using System.Text.RegularExpressions;
using TeleMed.Domain.Enums;

namespace TeleMed.Application.Notifications.Templates;

public static class TemplateKeys
{
    public const string BookingConfirmed = "booking_confirmed";
    public const string AppointmentCancelled = "appointment_cancelled";
    public const string AppointmentCancelledForDoctor = "appointment_cancelled_doctor";
    public const string Reminder24h = "reminder_24h";
    public const string Reminder1h = "reminder_1h";
    public const string PaymentFailed = "payment_failed";
    public const string PrescriptionReady = "prescription_ready";
    public const string MedicalReportReady = "medical_report_ready";
    public const string DoctorApplicationSubmitted = "doctor_application_submitted";
    public const string DoctorApplicationApproved = "doctor_application_approved";
    public const string DoctorApplicationRejected = "doctor_application_rejected";
    public const string RescheduleRequested = "reschedule_requested";
    public const string RescheduleConfirmed = "reschedule_confirmed";
    public const string RescheduleAcceptedForDoctor = "reschedule_accepted_doctor";
    public const string DoctorRunningLate = "doctor_running_late";
    public const string EarlyJoinOffered = "early_join_offered";
    public const string PaymentRefunded = "payment_refunded";
    public const string CustomerCareReply = "customer_care_reply";
}

public sealed record EmailText(string Subject, string Body);

public sealed partial record NotificationTemplate(
    string Key,
    IReadOnlyDictionary<Language, EmailText>? Email,
    IReadOnlyDictionary<Language, string>? Sms)
{
    public EmailText? RenderEmail(Language locale, IReadOnlyDictionary<string, string> values)
    {
        if (Email is null)
        {
            return null;
        }

        var text = Localized(Email, locale);
        return new EmailText(Render(text.Subject, values), Render(text.Body, values));
    }

    public string? RenderSms(Language locale, IReadOnlyDictionary<string, string> values) =>
        Sms is null ? null : Render(Localized(Sms, locale), values);

    private static T Localized<T>(IReadOnlyDictionary<Language, T> texts, Language locale) =>
        texts.TryGetValue(locale, out var text) ? text : texts[Language.En];

    private string Render(string text, IReadOnlyDictionary<string, string> values) =>
        Placeholder().Replace(text, m => values.TryGetValue(m.Groups[1].Value, out var value)
            ? value
            : throw new InvalidOperationException($"Template {Key} uses {{{m.Groups[1].Value}}}, which its model does not supply."));

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();
}

// Ported from telemed-backend migrations/notification 000003 and 000006-000010. Email bodies are plain text.
// Where the Go seed had no sms or email row, the push/in_app copy stands in, since those channels are gone.
// The *_doctor keys, payment_refunded and the applicant-facing doctor_application_submitted copy are new; their si/ta text reuses
// phrases from the seeded translations but has not been reviewed by a native speaker.
public static class NotificationTemplates
{
    private const string CancelledEmailFooterEn = "Open the appointment in the app for the full details.";
    private const string CancelledEmailFooterSi = "සම්පූර්ණ විස්තර සඳහා යෙදුමේ හමුව විවෘත කරන්න.";
    private const string CancelledEmailFooterTa = "முழு விவரங்களுக்கு செயலியில் சந்திப்பைத் திறக்கவும்.";

    public static IReadOnlyDictionary<string, NotificationTemplate> All { get; } = new NotificationTemplate[]
    {
        new(TemplateKeys.BookingConfirmed,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Your appointment with Dr. {DoctorName} is confirmed",
                    "Your appointment with Dr. {DoctorName} is confirmed for {DateTime}.\n\nFee: {Fee}\n\nYou will receive the link to join shortly before your appointment time."),
                [Language.Si] = new("ඔබගේ වෛද්‍ය {DoctorName} හමුව තහවුරු කර ඇත",
                    "ඔබගේ වෛද්‍ය {DoctorName} හමුව {DateTime} සඳහා තහවුරු කර ඇත.\n\nගාස්තුව: {Fee}\n\nහමුවීමේ වේලාවට පෙර සම්බන්ධ වීමේ විස්තර ඔබට එවනු ලැබේ."),
                [Language.Ta] = new("மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு உறுதி செய்யப்பட்டது",
                    "மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு {DateTime} அன்று உறுதி செய்யப்பட்டுள்ளது.\n\nகட்டணம்: {Fee}\n\nசந்திப்பு நேரத்திற்கு முன் இணையும் விவரங்கள் உங்களுக்கு அனுப்பப்படும்."),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Your appointment with Dr. {DoctorName} is confirmed for {DateTime}. Fee: {Fee}. Thank you for booking.",
                [Language.Si] = "ඔබගේ වෛද්‍ය {DoctorName} හමුව {DateTime} සඳහා තහවුරු කර ඇත. ගාස්තුව: {Fee}. වෙන් කිරීම සඳහා ස්තුතියි.",
                [Language.Ta] = "மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு {DateTime} அன்று உறுதி செய்யப்பட்டுள்ளது. கட்டணம்: {Fee}. முன்பதிவு செய்ததற்கு நன்றி.",
            }),

        new(TemplateKeys.AppointmentCancelled,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Your appointment has been cancelled",
                    "Your appointment with Dr. {DoctorName} on {DateTime} has been cancelled.\n\n" + CancelledEmailFooterEn),
                [Language.Si] = new("ඔබගේ හමුව අවලංගු කර ඇත",
                    "ඔබගේ වෛද්‍ය {DoctorName} හමුව {DateTime} සඳහා අවලංගු කර ඇත.\n\n" + CancelledEmailFooterSi),
                [Language.Ta] = new("உங்கள் சந்திப்பு ரத்து செய்யப்பட்டது",
                    "மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு {DateTime} அன்று ரத்து செய்யப்பட்டது.\n\n" + CancelledEmailFooterTa),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Your appointment with Dr. {DoctorName} on {DateTime} has been cancelled.",
                [Language.Si] = "ඔබගේ වෛද්‍ය {DoctorName} හමුව ({DateTime}) අවලංගු කර ඇත.",
                [Language.Ta] = "மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு ({DateTime}) ரத்து செய்யப்பட்டது.",
            }),

        new(TemplateKeys.AppointmentCancelledForDoctor,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("An appointment has been cancelled",
                    "Your appointment on {DateTime} has been cancelled.\n\n" + CancelledEmailFooterEn),
                [Language.Si] = new("ඔබගේ හමුව අවලංගු කර ඇත",
                    "ඔබගේ හමුව ({DateTime}) අවලංගු කර ඇත.\n\n" + CancelledEmailFooterSi),
                [Language.Ta] = new("உங்கள் சந்திப்பு ரத்து செய்யப்பட்டது",
                    "உங்கள் சந்திப்பு ({DateTime}) ரத்து செய்யப்பட்டது.\n\n" + CancelledEmailFooterTa),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Your appointment on {DateTime} has been cancelled.",
                [Language.Si] = "ඔබගේ හමුව ({DateTime}) අවලංගු කර ඇත.",
                [Language.Ta] = "உங்கள் சந்திப்பு ({DateTime}) ரத்து செய்யப்பட்டது.",
            }),

        new(TemplateKeys.Reminder24h,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Reminder: your appointment tomorrow with Dr. {DoctorName}",
                    "This is a reminder. Your appointment with Dr. {DoctorName} is scheduled for {DateTime}."),
                [Language.Si] = new("මතක් කිරීම: හෙට ඔබගේ වෛද්‍ය {DoctorName} හමුව",
                    "මෙය ඔබට මතක් කිරීමකි. ඔබගේ වෛද්‍ය {DoctorName} හමුව {DateTime} දින නියමිතව ඇත."),
                [Language.Ta] = new("நினைவூட்டல்: நாளை மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு",
                    "இது ஒரு நினைவூட்டல். மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு {DateTime} அன்று நிர்ணயிக்கப்பட்டுள்ளது."),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Your appointment with Dr. {DoctorName} is on {DateTime}.",
                [Language.Si] = "ඔබගේ වෛද්‍ය {DoctorName} හමුව {DateTime} දින නියමිතය.",
                [Language.Ta] = "மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு {DateTime} அன்று நிர்ணயிக்கப்பட்டுள்ளது.",
            }),

        new(TemplateKeys.Reminder1h,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Appointment in 1 hour", "Your appointment with Dr. {DoctorName} starts in 1 hour."),
                [Language.Si] = new("පැයකින් හමුවක්", "ඔබගේ වෛද්‍ය {DoctorName} හමුව පැයකින් ආරම්භ වේ."),
                [Language.Ta] = new("ஒரு மணி நேரத்தில் சந்திப்பு", "மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு ஒரு மணி நேரத்தில் தொடங்குகிறது."),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Reminder: your appointment with Dr. {DoctorName} starts in 1 hour. Please be ready to join.",
                [Language.Si] = "මතක් කිරීමයි: ඔබගේ වෛද්‍ය {DoctorName} හමුව පැයකින් ආරම්භ වේ. කරුණාකර සූදානම් වන්න.",
                [Language.Ta] = "நினைவூட்டல்: மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு ஒரு மணி நேரத்தில் தொடங்குகிறது. தயவுசெய்து தயாராக இருங்கள்.",
            }),

        new(TemplateKeys.PaymentFailed,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Payment failed", "Your payment of {Amount} could not be processed. Please try again to confirm your appointment."),
                [Language.Si] = new("ගෙවීම අසාර්ථකයි", "ඔබගේ {Amount} ගෙවීම සිදු කළ නොහැකි විය. හමුව තහවුරු කිරීමට කරුණාකර නැවත උත්සාහ කරන්න."),
                [Language.Ta] = new("கட்டணம் தோல்வியடைந்தது", "உங்கள் {Amount} கட்டணத்தைச் செலுத்த முடியவில்லை. சந்திப்பை உறுதி செய்ய மீண்டும் முயற்சிக்கவும்."),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Your payment of {Amount} could not be processed. Please try again to confirm your appointment.",
                [Language.Si] = "ඔබගේ {Amount} ගෙවීම සිදු කළ නොහැකි විය. හමුව තහවුරු කිරීමට කරුණාකර නැවත උත්සාහ කරන්න.",
                [Language.Ta] = "உங்கள் {Amount} கட்டணத்தைச் செலுத்த முடியவில்லை. சந்திப்பை உறுதி செய்ய மீண்டும் முயற்சிக்கவும்.",
            }),

        new(TemplateKeys.PrescriptionReady,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Your prescription is ready",
                    "Your prescription from Dr. {DoctorName} is ready. The PDF is attached to this email.\n\nView or download it in VersaLife: {DownloadUrl}"),
                [Language.Si] = new("ඔබගේ බෙහෙත් වට්ටෝරුව සූදානම්",
                    "ඔබගේ වෛද්‍ය {DoctorName} විසින් නිකුත් කළ බෙහෙත් වට්ටෝරුව සූදානම්. PDF එක මෙම ඊමේල් සමඟ අමුණා ඇත.\n\nVersaLife හි බලන්න හෝ බාගත කරන්න: {DownloadUrl}"),
                [Language.Ta] = new("உங்கள் மருந்துச் சீட்டு தயார்",
                    "மருத்துவர் {DoctorName} வழங்கிய உங்கள் மருந்துச் சீட்டு தயார். PDF இந்த மின்னஞ்சலுடன் இணைக்கப்பட்டுள்ளது.\n\nVersaLife-இல் பார்க்கவும் அல்லது பதிவிறக்கவும்: {DownloadUrl}"),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Your prescription from Dr. {DoctorName} is ready. Download: {DownloadUrl}",
                [Language.Si] = "ඔබගේ වෛද්‍ය {DoctorName} විසින් නිකුත් කළ බෙහෙත් වට්ටෝරුව සූදානම්. බාගත කරන්න: {DownloadUrl}",
                [Language.Ta] = "மருத்துவர் {DoctorName} வழங்கிய உங்கள் மருந்துச் சீட்டு தயார். பதிவிறக்கம்: {DownloadUrl}",
            }),

        new(TemplateKeys.MedicalReportReady,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Your medical report is ready",
                    "Your medical report from Dr. {DoctorName} is ready. The PDF is attached to this email.\n\nView or download it in VersaLife: {DownloadUrl}"),
                [Language.Si] = new("ඔබගේ වෛද්‍ය වාර්තාව සූදානම්",
                    "ඔබගේ වෛද්‍ය {DoctorName} විසින් නිකුත් කළ වෛද්‍ය වාර්තාව සූදානම්. PDF එක මෙම ඊමේල් සමඟ අමුණා ඇත.\n\nVersaLife හි බලන්න හෝ බාගත කරන්න: {DownloadUrl}"),
                [Language.Ta] = new("உங்கள் மருத்துவ அறிக்கை தயார்",
                    "மருத்துவர் {DoctorName} வழங்கிய உங்கள் மருத்துவ அறிக்கை தயார். PDF இந்த மின்னஞ்சலுடன் இணைக்கப்பட்டுள்ளது.\n\nVersaLife-இல் பார்க்கவும் அல்லது பதிவிறக்கவும்: {DownloadUrl}"),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Your medical report from Dr. {DoctorName} is ready. Download: {DownloadUrl}",
                [Language.Si] = "ඔබගේ වෛද්‍ය {DoctorName} විසින් නිකුත් කළ වෛද්‍ය වාර්තාව සූදානම්. බාගත කරන්න: {DownloadUrl}",
                [Language.Ta] = "மருத்துவர் {DoctorName} வழங்கிய உங்கள் மருத்துவ அறிக்கை தயார். பதிவிறக்கம்: {DownloadUrl}",
            }),

        new(TemplateKeys.DoctorApplicationSubmitted,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("We received your doctor application",
                    "Dear Dr. {DoctorName},\n\nThank you for applying. We will review your application and email you once a decision is made."),
                [Language.Si] = new("ඔබගේ වෛද්‍ය අයදුම්පත ලැබුණි",
                    "ගරු වෛද්‍ය {DoctorName},\n\nඔබගේ අයදුම්පත අපට ලැබුණි. අපි එය සමාලෝචනය කර තීරණයක් ගත් පසු ඔබට ඊමේල් කරන්නෙමු."),
                [Language.Ta] = new("உங்கள் மருத்துவர் விண்ணப்பம் பெறப்பட்டது",
                    "மதிப்பிற்குரிய மருத்துவர் {DoctorName} அவர்களே,\n\nஉங்கள் விண்ணப்பம் எங்களுக்குக் கிடைத்தது. அதை மதிப்பாய்வு செய்து முடிவு எடுக்கப்பட்டவுடன் உங்களுக்கு மின்னஞ்சல் அனுப்புவோம்."),
            },
            Sms: null),

        new(TemplateKeys.DoctorApplicationApproved,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Your doctor application was approved",
                    "Dear Dr. {DoctorName},\n\nYour application has been approved. Sign in at the doctor portal with your phone number and complete OTP verification to activate your account."),
                [Language.Si] = new("ඔබගේ වෛද්‍ය අයදුම්පත අනුමතයි",
                    "ගරු වෛද්‍ය {DoctorName},\n\nඔබගේ අයදුම්පත අනුමත කර ඇත. වෛද්‍ය ද්වාරයේ දුරකථන අංකයෙන් පිවිස OTP සම්පූර්ණ කර ගිණුම සක්‍රිය කරන්න."),
                [Language.Ta] = new("உங்கள் மருத்துவர் விண்ணப்பம் அங்கீகரிக்கப்பட்டது",
                    "மதிப்பிற்குரிய மருத்துவர் {DoctorName} அவர்களே,\n\nஉங்கள் விண்ணப்பம் அங்கீகரிக்கப்பட்டது. மருத்துவர் போர்ட்டலில் தொலைபேசி எண்ணால் உள்நுழைந்து OTP சரிபார்த்து கணக்கை செயல்படுத்தவும்."),
            },
            Sms: null),

        new(TemplateKeys.DoctorApplicationRejected,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Your doctor application needs attention",
                    "Dear Dr. {DoctorName},\n\nYour application was not approved this time. Reason: {Reason}\n\nPlease contact us for further details."),
                [Language.Si] = new("ඔබගේ වෛද්‍ය අයදුම්පත සම්බන්ධයෙන් අවධානය අවශ්‍යයි",
                    "ගරු වෛද්‍ය {DoctorName},\n\nඔබගේ අයදුම්පත මෙවර අනුමත කළ නොහැකි විය. හේතුව: {Reason}"),
                [Language.Ta] = new("உங்கள் மருத்துவர் விண்ணப்பத்திற்கு கவனம் தேவை",
                    "மதிப்பிற்குரிய மருத்துவர் {DoctorName} அவர்களே,\n\nஉங்கள் விண்ணப்பம் இந்த முறை அங்கீகரிக்கப்படவில்லை. காரணம்: {Reason}"),
            },
            Sms: null),

        new(TemplateKeys.RescheduleRequested,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Your visit with Dr. {DoctorName} may move",
                    "Dr. {DoctorName} cannot attend your visit at {DateTime} and has asked to move it to {ProposedDateTime}.\n\nOpen the app to accept the new time, or decline for a full refund."),
                [Language.Si] = new("වෛද්‍ය {DoctorName} සමඟ ඔබගේ හමුව ගෙන යා හැක",
                    "වෛද්‍ය {DoctorName}ට {DateTime} හමුවට පැමිණිය නොහැකි අතර එය {ProposedDateTime} දක්වා වෙනස් කිරීමට ඉල්ලා ඇත.\n\nයෙදුමෙන් නව වේලාව පිළිගන්න, නැතහොත් සම්පූර්ණ ආපසු ගෙවීමක් සඳහා ප්‍රතික්ෂේප කරන්න."),
                [Language.Ta] = new("மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு மாறலாம்",
                    "மருத்துவர் {DoctorName} {DateTime} சந்திப்பில் கலந்துகொள்ள முடியாது, அதை {ProposedDateTime}க்கு மாற்றக் கேட்டுள்ளார்.\n\nசெயலியில் புதிய நேரத்தை ஏற்கவும், அல்லது முழுத் திருப்பிச் செலுத்தலுக்கு நிராகரிக்கவும்."),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Dr. {DoctorName} asked to move your visit from {DateTime} to {ProposedDateTime}. Open the app to accept or decline (full refund).",
                [Language.Si] = "වෛද්‍ය {DoctorName} ඔබගේ හමුව {DateTime} සිට {ProposedDateTime} දක්වා වෙනස් කිරීමට ඉල්ලා ඇත. යෙදුමෙන් පිළිගන්න හෝ ප්‍රතික්ෂේප කරන්න (සම්පූර්ණ ආපසු ගෙවීම).",
                [Language.Ta] = "மருத்துவர் {DoctorName} உங்கள் சந்திப்பை {DateTime} இலிருந்து {ProposedDateTime}க்கு மாற்றக் கேட்டுள்ளார். செயலியில் ஏற்கவும் அல்லது நிராகரிக்கவும் (முழுத் திருப்பிச் செலுத்தல்).",
            }),

        new(TemplateKeys.RescheduleConfirmed,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Your visit with Dr. {DoctorName} is now at {DateTime}",
                    "The new time for your visit with Dr. {DoctorName} is {DateTime}.\n\nYour payment is unchanged. The same join link still works."),
                [Language.Si] = new("වෛද්‍ය {DoctorName} සමඟ ඔබගේ හමුව දැන් {DateTime}ට ය",
                    "වෛද්‍ය {DoctorName} සමඟ ඔබගේ හමුවේ නව වේලාව {DateTime} ය.\n\nඔබගේ ගෙවීම වෙනස් නොවේ. එම සම්බන්ධ වීමේ සබැඳියම තවමත් ක්‍රියා කරයි."),
                [Language.Ta] = new("மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு இப்போது {DateTime}",
                    "மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பின் புதிய நேரம் {DateTime}.\n\nஉங்கள் கட்டணம் மாறவில்லை. அதே இணைப்பு இணைப்பு இன்னும் வேலை செய்கிறது."),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Your visit with Dr. {DoctorName} is now at {DateTime}.",
                [Language.Si] = "වෛද්‍ය {DoctorName} සමඟ ඔබගේ හමුව දැන් {DateTime}ට ය.",
                [Language.Ta] = "மருத்துவர் {DoctorName} உடனான உங்கள் சந்திப்பு இப்போது {DateTime}.",
            }),

        new(TemplateKeys.RescheduleAcceptedForDoctor,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Visit time updated", "The patient accepted your proposed time. The visit is now at {DateTime}."),
                [Language.Si] = new("හමුවේ වේලාව යාවත්කාලීනයි", "ඔබගේ හමුව දැන් {DateTime}ට ය."),
                [Language.Ta] = new("சந்திப்பு நேரம் புதுப்பிக்கப்பட்டது", "உங்கள் சந்திப்பு இப்போது {DateTime}."),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "The patient accepted your proposed time. The visit is now at {DateTime}.",
                [Language.Si] = "ඔබගේ හමුව දැන් {DateTime}ට ය.",
                [Language.Ta] = "உங்கள் சந்திப்பு இப்போது {DateTime}.",
            }),

        new(TemplateKeys.DoctorRunningLate,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Dr. {DoctorName} is running a little late",
                    "Dr. {DoctorName} is currently with the previous patient.\n\nPlease stay with us — we are sorry for the short delay. Your visit will begin shortly."),
                [Language.Si] = new("වෛද්‍ය {DoctorName} ටිකක් ප්‍රමාදයි",
                    "වෛද්‍ය {DoctorName} දැන් පෙර රෝගීන් සමඟ ඇත.\n\nකරුණාකර අප සමඟ සිටින්න — කෙටි ප්‍රමාදයට කණගාටුයි. ඔබගේ හමුව ඉක්මනින් ආරම්භ වේ."),
                [Language.Ta] = new("மருத்துவர் {DoctorName} சிறிது தாமதம்",
                    "மருத்துவர் {DoctorName} தற்போது முந்தைய நோயாளியுடன் உள்ளார்.\n\nதயவுசெய்து எங்களுடன் இருங்கள் — சிறு தாமதத்திற்கு வருந்துகிறோம். உங்கள் சந்திப்பு விரைவில் தொடங்கும்."),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Dr. {DoctorName} is currently with the previous patient. Please stay with us — we are sorry for the short delay.",
                [Language.Si] = "වෛද්‍ය {DoctorName} දැන් පෙර රෝගීන් සමඟ ඇත. කරුණාකර අප සමඟ සිටින්න — කෙටි ප්‍රමාදයට කණගාටුයි.",
                [Language.Ta] = "மருத்துவர் {DoctorName} தற்போது முந்தைய நோயாளியுடன் உள்ளார். தயவுசெய்து எங்களுடன் இருங்கள் — சிறு தாமதத்திற்கு வருந்துகிறோம்.",
            }),

        new(TemplateKeys.EarlyJoinOffered,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Dr. {DoctorName} is free a few minutes early",
                    "Dr. {DoctorName} is free a few minutes early and can see you now if you are ready.\n\nJoin now: {JoinLink}\n\nIf now is not convenient, keep your original booked time — nothing else changes."),
                [Language.Si] = new("වෛද්‍ය {DoctorName} මිනිත්තු කිහිපයකින් කලින් නිදහස්",
                    "වෛද්‍ය {DoctorName} මිනිත්තු කිහිපයකින් කලින් නිදහස්. ඔබ සූදානම් නම් දැන් එකතු විය හැකිය.\n\nදැන් සම්බන්ධ වන්න: {JoinLink}\n\nදැන් අපහසු නම්, වෙන්කළ වේලාව තබා ගන්න — වෙනත් කිසිවක් වෙනස් නොවේ."),
                [Language.Ta] = new("மருத்துவர் {DoctorName} சில நிமிடங்கள் முன்னதாக காலியாக இருக்கிறார்",
                    "மருத்துவர் {DoctorName} சில நிமிடங்கள் முன்னதாக காலியாக இருக்கிறார். நீங்கள் தயாராக இருந்தால் இப்போது இணையலாம்.\n\nஇப்போது இணையவும்: {JoinLink}\n\nஇப்போது வசதியாக இல்லையென்றால், உங்கள் முன்பதிவு நேரத்தை வைத்துக்கொள்ளுங்கள் — வேறு எதுவும் மாறாது."),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Dr. {DoctorName} is free a few minutes early. Can you join now? {JoinLink} — or keep your booked time.",
                [Language.Si] = "වෛද්‍ය {DoctorName} මිනිත්තු කිහිපයකින් කලින් නිදහස්. දැන් එකතු විය හැකිද? {JoinLink} — නැතිනම් වෙන්කළ වේලාව තබා ගන්න.",
                [Language.Ta] = "மருத்துவர் {DoctorName} சில நிமிடங்கள் முன்னதாக காலியாக இருக்கிறார். இப்போது இணைய முடியுமா? {JoinLink} — அல்லது முன்பதிவு நேரத்தை வைத்துக்கொள்ளுங்கள்.",
            }),

        new(TemplateKeys.PaymentRefunded,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Your payment has been refunded",
                    "Your payment of {Amount} arrived after your booking had expired and the appointment could not be confirmed, so it has been refunded in full.\n\nIt may take a few working days to reach your account."),
                [Language.Si] = new("ඔබගේ ගෙවීම ආපසු ගෙවා ඇත",
                    "ඔබගේ {Amount} ගෙවීම වෙන් කිරීම කල් ඉකුත් වූ පසු ලැබුණු බැවින් හමුව තහවුරු කළ නොහැකි විය. එබැවින් මුළු මුදලම ආපසු ගෙවා ඇත.\n\nඔබගේ ගිණුමට ළඟා වීමට වැඩ කරන දින කිහිපයක් ගත විය හැකිය."),
                [Language.Ta] = new("உங்கள் கட்டணம் திருப்பி அளிக்கப்பட்டது",
                    "உங்கள் முன்பதிவு காலாவதியான பிறகு உங்கள் {Amount} கட்டணம் கிடைத்ததால் சந்திப்பை உறுதி செய்ய முடியவில்லை. எனவே முழுத் தொகையும் திருப்பி அளிக்கப்பட்டது.\n\nஉங்கள் கணக்கை அடைய சில வேலை நாட்கள் ஆகலாம்."),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Your payment of {Amount} arrived after your booking expired, so it has been refunded in full. It may take a few working days to appear.",
                [Language.Si] = "ඔබගේ {Amount} ගෙවීම වෙන් කිරීම කල් ඉකුත් වූ පසු ලැබුණු බැවින් මුළු මුදලම ආපසු ගෙවා ඇත. ගිණුමට ළඟා වීමට දින කිහිපයක් ගත විය හැකිය.",
                [Language.Ta] = "உங்கள் முன்பதிவு காலாவதியான பிறகு {Amount} கட்டணம் கிடைத்ததால் முழுத் தொகையும் திருப்பி அளிக்கப்பட்டது. கணக்கில் தோன்ற சில நாட்கள் ஆகலாம்.",
            }),
        new(TemplateKeys.CustomerCareReply,
            Email: new Dictionary<Language, EmailText>
            {
                [Language.En] = new("Customer care replied: {Subject}",
                    "VersaLife customer care replied to your message about {Subject}.\n\nOpen the app and tap the chat button in the bottom-right corner to continue the conversation."),
                [Language.Si] = new("පාරිභෝගික සේවය පිළිතුරු දුන්නේය: {Subject}",
                    "VersaLife පාරිභෝගික සේවය ඔබගේ {Subject} පණිවිඩයට පිළිතුරු දී ඇත.\n\nඅඛණ්ඩව කතාබස් කිරීමට යෙදුම විවෘත කර පහළ දකුණු කෙළවරේ ඇති චැට් බොත්තම ඔබන්න."),
                [Language.Ta] = new("வாடிக்கையாளர் சேவை பதிலளித்துள்ளது: {Subject}",
                    "VersaLife வாடிக்கையாளர் சேவை உங்கள் {Subject} செய்திக்கு பதிலளித்துள்ளது.\n\nஉரையாடலைத் தொடர செயலியைத் திறந்து கீழ் வலது மூலையிலுள்ள அரட்டை பொத்தானைத் தட்டவும்."),
            },
            Sms: new Dictionary<Language, string>
            {
                [Language.En] = "Customer care replied about {Subject}. Open the app and tap the chat button to continue.",
                [Language.Si] = "පාරිභෝගික සේවය {Subject} ගැන පිළිතුරු දුන්නේය. යෙදුමේ චැට් බොත්තම ඔබන්න.",
                [Language.Ta] = "வாடிக்கையாளர் சேவை {Subject} குறித்து பதிலளித்துள்ளது. செயலியில் அரட்டை பொத்தானைத் தட்டவும்.",
            }),
    }.ToDictionary(t => t.Key);
}
