using System.Text.RegularExpressions;

namespace VoceLocale.Core;

public static class MeetingNotesPolicy
{
    private const string Days = @"\b(?:lunedì|martedì|mercoledì|giovedì|venerdì|sabato|domenica|monday|tuesday|wednesday|thursday|friday|saturday|sunday)\b";
    public static bool TryValidate(string source, string? candidate, out string notes)
    {
        notes = "";
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(candidate) || candidate.Length > 8000
            || candidate.Contains("<think", StringComparison.OrdinalIgnoreCase) || candidate.Contains("<|", StringComparison.Ordinal)) return false;
        static HashSet<string> Tokens(string text, string pattern) => Regex.Matches(text, pattern, RegexOptions.IgnoreCase)
            .Select(m => m.Value.ToLowerInvariant().Replace(',', '.')).ToHashSet();
        foreach (string pattern in new[] { Days })
        {
            var before = Tokens(source, pattern); var after = Tokens(candidate, pattern);
            if (!before.SetEquals(after)) return false;
        }
        if (!TranslationPolicy.Values(source).ToHashSet().SetEquals(TranslationPolicy.Values(candidate))) return false;
        const string negative = @"\b(?:non|no|mai|senza|nessun|nessuna|nessuno|not|never|without|neither|none)\b|n['’]t\b";
        if (Regex.IsMatch(source, negative, RegexOptions.IgnoreCase) && !Regex.IsMatch(candidate, negative, RegexOptions.IgnoreCase)) return false;
        const string future = @"\b(?:will|shall|sarà|saranno|verrà|verranno|farà|faremo|farete|avrà|avranno|[\p{L}]+erà|[\p{L}]+irà|[\p{L}]+eranno|[\p{L}]+iranno)\b";
        const string commitment = @"\b(?:si incarica|si impegna|da fare|action items?|next steps?)\b";
        if (Regex.IsMatch(source, future, RegexOptions.IgnoreCase) && !Regex.IsMatch(candidate, future + "|" + commitment, RegexOptions.IgnoreCase)) return false;
        const string completed = @"\b(?:ha inviato|ha preparato|ha completato|has sent|has prepared|has completed|already completed)\b";
        if (Regex.IsMatch(source, future, RegexOptions.IgnoreCase) && !Regex.IsMatch(source, completed, RegexOptions.IgnoreCase)
            && Regex.IsMatch(candidate, completed, RegexOptions.IgnoreCase)) return false;
        // Queste parole introducono frasi o etichette; non sono indizi di nomi propri.
        var discourse = new HashSet<string>(["Forse", "Perhaps", "Maybe", "All", "Dopo", "Nessuna", "Nessuno", "Nessun", "APPUNTI", "PERSONALI", "DECISIONE", "CONFERMATA"], StringComparer.OrdinalIgnoreCase);
        foreach (string name in TextEntities.NamedWords(source).Where(name => !discourse.Contains(name)))
            if (!Regex.IsMatch(candidate, @"\b" + Regex.Escape(name) + @"\b", RegexOptions.IgnoreCase)) return false;
        const string uncertain = @"\b(?:forse|probabilmente|potrebbe|potrebbero|potrà|maybe|perhaps|probably|might|may)\b";
        if (Regex.IsMatch(source, uncertain, RegexOptions.IgnoreCase) && !Regex.IsMatch(candidate, uncertain, RegexOptions.IgnoreCase)) return false;
        foreach (Match question in Regex.Matches(source, @"\bchiedere a\s+([\p{Lu}][\p{L}]+)", RegexOptions.IgnoreCase))
        {
            if (Regex.IsMatch(candidate, @"\bda parte di\s+" + Regex.Escape(question.Groups[1].Value) + @"\b", RegexOptions.IgnoreCase)) return false;
            if (!Regex.IsMatch(source, @"\bchiest[oa]\s+a\b", RegexOptions.IgnoreCase)
                && Regex.IsMatch(candidate, @"\bchiest[oa]\s+a\s+" + Regex.Escape(question.Groups[1].Value) + @"\b", RegexOptions.IgnoreCase)) return false;
        }
        notes = candidate.Trim(); return true;
    }

    // This fallback quotes the supplied transcript; it cannot invent facts or obey its commands.
    public static string Extract(string excerpt) => string.Join("\n", excerpt.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => "- " + line));
}
