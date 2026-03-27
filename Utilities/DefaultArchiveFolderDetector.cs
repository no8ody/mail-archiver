using System.Globalization;
using System.Text;

namespace MailArchiver.Utilities;

public static class DefaultArchiveFolderDetector
{
    private static readonly HashSet<string> InboxAliases = new(GetInboxAliases().Select(NormalizeAlias), StringComparer.Ordinal);
    private static readonly HashSet<string> SentAliases = new(GetSentAliases().Select(NormalizeAlias), StringComparer.Ordinal);

    public static bool IsDefaultArchiveFolder(string? folderName)
        => IsInboxFolder(folderName) || IsSentFolder(folderName);

    public static bool IsInboxFolder(string? folderName)
        => InboxAliases.Contains(GetNormalizedLeaf(folderName));

    public static bool IsSentFolder(string? folderName)
        => SentAliases.Contains(GetNormalizedLeaf(folderName));

    public static string GetFolderLeafName(string? folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return string.Empty;
        }

        var normalized = folderName.Trim().Replace('\\', '/');
        var parts = normalized
            .Split(new[] { '/', '.', '>', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? normalized : parts[^1];
    }

    private static string GetNormalizedLeaf(string? folderName)
        => NormalizeAlias(GetFolderLeafName(folderName));

    private static string NormalizeAlias(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                sb.Append(' ');
                previousWasSpace = true;
            }
        }

        return sb.ToString().Trim();
    }

    private static IEnumerable<string> GetInboxAliases()
    {
        return new[]
        {
            // English
            "inbox",

            // German
            "posteingang", "eingang",

            // French
            "boîte de réception", "boite de réception", "réception", "courrier entrant",

            // Spanish
            "bandeja de entrada", "entrada",

            // Italian
            "posta in arrivo", "in arrivo", "arrivo",

            // Dutch
            "postvak in", "inkomend",

            // Polish
            "skrzynka odbiorcza", "odebrane",

            // Russian
            "входящие", "папка входящие",

            // Slovenian
            "prejeto", "prejeta pošta", "prejeta posta",

            // Hungarian
            "beérkezett üzenetek", "bejövő üzenetek", "bejövő", "beérkezett", "beerkezett", "bejovo",

            // Additional common server/localization variants
            "caixa de entrada", "doručená pošta", "prijatá pošta", "indbakke", "innboks", "inkorg",
            "saapuneet", "saapuneet viestit", "sisendkaust", "mesaje primite", "primite",
            "gelen kutusu", "εισερχόμενα", "דואר נכנס", "البريد الوارد",
            "受信トレイ", "받은편지함", "收件箱", "收件匣"
        };
    }

    private static IEnumerable<string> GetSentAliases()
    {
        return new[]
        {
            // English
            "sent", "sent items", "sent mail", "sent messages",

            // German
            "gesendet", "gesendete", "gesendete objekte", "gesendete elemente", "gesendete nachrichten",

            // French
            "envoyé", "envoyés", "éléments envoyés", "elements envoyes", "messages envoyés", "messages envoyes", "courrier envoyé", "courrier envoye",

            // Spanish
            "enviado", "enviados", "elementos enviados", "correo enviado", "mensajes enviados",

            // Italian
            "inviato", "inviati", "posta inviata", "elementi inviati", "messaggi inviati",

            // Dutch
            "verzonden", "verzonden items", "verzonden e-mail", "verzonden e-mails",

            // Polish
            "wysłane", "wyslane", "elementy wysłane", "elementy wyslane", "poczta wysłana", "poczta wyslana",

            // Russian
            "отправленные", "исходящие", "отправлено",

            // Slovenian
            "poslano", "poslana pošta", "poslana posta",

            // Hungarian
            "elküldött", "elkuldott", "elküldött elemek", "elkuldott elemek",

            // Additional common server/localization variants
            "المرسلة", "البريد المرسل", "изпратени", "изпратена поща", "已发送", "已传送",
            "odeslané", "odeslaná pošta", "sendt", "sendte elementer", "saadetud", "saadetud kirjad",
            "lähetetyt", "lahetetyt", "lähetetyt kohteet", "lahetetyt kohteet", "απεσταλμένα", "απεσταλμενα",
            "σταλμένα", "σταλμενα", "σταλμένα μηνύματα", "σταλμενα μηνυματα", "נשלחו", "דואר יוצא",
            "seolta", "r-phost seolta", "送信済み", "送信済メール", "送信メール", "보낸편지함",
            "발신함", "보낸메일", "nosūtītie", "nosūtītās vēstules", "nosutitas vestules", "išsiųsta",
            "issiusta", "išsiųsti laiškai", "issiusti laiskai", "mibgħuta", "posta mibgħuta", "trimise",
            "elemente trimise", "mail trimis", "odoslané", "odoslaná pošta", "skickat", "skickade objekt",
            "gönderilen", "gonderilen", "gönderilmiş öğeler", "gonderilmis ogeler"
        };
    }
}
