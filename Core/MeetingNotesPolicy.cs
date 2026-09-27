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
        const string negative = @"\b(?:non|no|mai|senza|not|never|without)\b|n['’]t\b";
        if (Regex.IsMatch(source, negative, RegexOptions.IgnoreCase) && !Regex.IsMatch(candidate, negative, RegexOptions.IgnoreCase)) return false;
        const string future = @"\b(?:will|shall|sarà|saranno|verrà|verranno|invierà|invieranno|farà|faremo|farete|faremo|avrà|avranno)\b";
        if (Regex.IsMatch(source, future, RegexOptions.IgnoreCase) && !Regex.IsMatch(candidate, future, RegexOptions.IgnoreCase)) return false;
        foreach (string name in TextEntities.NamedWords(source))
            if (!Regex.IsMatch(candidate, @"\b" + Regex.Escape(name) + @"\b", RegexOptions.IgnoreCase)) return false;
        const string uncertain = @"\b(?:forse|probabilmente|potrebbe|potrebbero|maybe|perhaps|probably|might)\b";
        if (Regex.IsMatch(source, uncertain, RegexOptions.IgnoreCase) && !Regex.IsMatch(candidate, uncertain, RegexOptions.IgnoreCase)) return false;
        notes = candidate.Trim(); return true;
    }

    // This fallback quotes the supplied transcript; it cannot invent facts or obey its commands.
    public static string Extract(string excerpt) => string.Join("\n", excerpt.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(line => "- " + line));
}
