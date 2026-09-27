using System.Text.RegularExpressions;

namespace VoceLocale.Core;

/// <summary>Resolves only explicit same-category replacements; ambiguous edits remain with the model.</summary>
public static class DictationEdits
{
    private const string Day = @"(?:lunedì|martedì|mercoledì|giovedì|venerdì|sabato|domenica|Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)";
    private const string Number = @"(?:\d+(?:[.,]\d+)?|zero|uno|due|tre|quattro|cinque|sei|sette|otto|nove|dieci|undici|dodici|tredici|quattordici|quindici|sedici|diciassette|diciotto|diciannove|venti|trenta|quaranta|cinquanta|sessanta|settanta|ottanta|novanta|cento|one|two|three|four|five|six|seven|eight|nine|ten|fifteen|twenty|thirty|forty|fifty|eighty|hundred)";
    private const string Marker = @"(?:no\s*[,;:]?\s*(?:intendevo\s+|I\s+meant\s+)?|anzi\s+|scusa\s*[,;:]?\s*(?:intendevo\s+)?|sorry\s*[,;:]?\s*|actually\s*[,;:]?\s*|intendevo\s+|I\s+meant\s+)";
    private static readonly Regex Days = Replacement(Day);
    private static readonly Regex Numbers = Replacement(Number);
    private static Regex Replacement(string value) => new(@"\b(?<old>" + value + @")\b\s*[,.;:]\s*" + Marker + @"(?<new>" + value + @")\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool TryResolve(string original, string[]? vocabulary, out string result)
    {
        result = "";
        if (original.Contains('"') || original.Contains('“') || original.Contains('`')) return false;
        if (!TextPolicy.HasCorrection(original)) return false;
        string edited = original;
        for (int i = 0; i < 8; i++)
        {
            string before = edited;
            edited = Days.Replace(edited, "${new}");
            edited = Numbers.Replace(edited, m =>
                Regex.IsMatch(edited[..m.Index], @"(?:[-−/]|\b(?:meno|minus|negative)\s+)\s*$", RegexOptions.IgnoreCase)
                    ? m.Value : m.Groups["new"].Value);
            // A recipient correction must preserve the same preposition on both sides.
            var names = new Regex(@"\b(?<pre>a|to)\s+(?<old>[\p{Lu}][\p{L}'’]+)\s*[,.;:]\s*" + Marker + @"\k<pre>\s+(?<new>[\p{Lu}][\p{L}'’]+)\b", RegexOptions.CultureInvariant);
            edited = names.Replace(edited, m => $"{m.Groups["pre"].Value} {m.Groups["new"].Value}");
            if (edited == before) break;
        }
        if (edited == original || TextPolicy.HasCorrection(edited) || Regex.IsMatch(edited, @"\b(?:ehm|uhm|um|uh)\b", RegexOptions.IgnoreCase)) return false;
        // Only matched typed replacements changed: a sequence of corrections can legitimately
        // remove most input words, so validate the remaining literal sentence against itself.
        return TextPolicy.TryValidate(edited, edited, false, out result);
    }
}
