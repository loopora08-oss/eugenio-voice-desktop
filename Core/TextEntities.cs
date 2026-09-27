using System.Text.RegularExpressions;

namespace VoceLocale.Core;

public static class TextEntities
{
    private static readonly HashSet<string> Ordinary = new(("a an the this that these those i we you he she it they let let's " +
        "is are am was were be have has had do does did will shall please there if but and or on at in our your my " +
        "il la lo le gli un uno una non no sì sono sei siamo siete ci vi mi ti si scrivi quanto ignora salva " +
        "se per prima dopo domani ieri oggi sto ho hai abbiamo grazie buongiorno ciao buon manda invia " +
        "zero due tre quattro cinque sette otto nove dieci undici dodici tredici quattordici quindici sedici " +
        "diciassette diciotto diciannove venti trenta quaranta cinquanta sessanta settanta ottanta novanta " +
        "lunedì martedì mercoledì giovedì venerdì sabato domenica monday tuesday wednesday thursday friday saturday sunday " +
        "ehm uhm um uh hmm gennaio febbraio marzo aprile maggio giugno luglio agosto settembre ottobre novembre dicembre " +
        "january february march april may june july august september october november december").Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

    // Conservative hints, not a semantic proof. Unknown capitalized words are retained
    // rather than trusting an AI summary which silently changes or drops a person.
    public static string[] NamedWords(string text) => Regex.Matches(text, @"\b[\p{Lu}][\p{L}\p{N}]{2,}\b")
        .Select(m => m.Value).Where(word => !Ordinary.Contains(word)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
