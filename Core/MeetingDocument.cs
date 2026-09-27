using System.Text;
using System.Text.RegularExpressions;

namespace VoceLocale.Core;

public sealed record MeetingEntry(long Sequence, double StartSeconds, double EndSeconds,
    string Original, string? English = null, string? Warning = null, bool TranslationRequested = false, string? Language = null);

public sealed class MeetingDocument
{
    public int FormatVersion { get; set; } = 1;
    public string Title { get; set; } = "Meeting Eugenio";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public double DurationSeconds { get; set; }
    public List<MeetingEntry> Entries { get; set; } = [];
    public string Notes { get; set; } = "";
    public int? NotedEntries { get; set; }

    public bool IsValid()
    {
        // Also bound TimeSpan conversions before replacing an open document.
        const double maximumSeconds = 36525d * 24 * 60 * 60;
        return FormatVersion == 1 && Title != null && Notes != null && Entries != null && Entries.Count <= 100000
            && (NotedEntries == null || NotedEntries >= 0 && NotedEntries <= Entries.Count)
            && double.IsFinite(DurationSeconds) && DurationSeconds >= 0 && DurationSeconds <= maximumSeconds
            && Entries.All(e => e != null && e.Original != null && double.IsFinite(e.StartSeconds) && double.IsFinite(e.EndSeconds)
                && e.StartSeconds >= 0 && e.EndSeconds >= e.StartSeconds && e.EndSeconds <= maximumSeconds);
    }

    public string Markdown()
    {
        var text = new StringBuilder().Append("# ").AppendLine(Title).AppendLine()
            .Append("Data: ").AppendLine(CreatedAt.ToString("yyyy-MM-dd HH:mm zzz"))
            .Append("Durata: ").AppendLine(TimeSpan.FromSeconds(DurationSeconds).ToString(@"hh\:mm\:ss"))
            .AppendLine().AppendLine("## Appunti").AppendLine().AppendLine(Notes)
            .AppendLine().AppendLine("## Trascrizione").AppendLine();
        foreach (var entry in Entries)
        {
            text.Append("[").Append(TimeSpan.FromSeconds(entry.StartSeconds).ToString(@"hh\:mm\:ss"))
                .Append("] ").AppendLine(entry.Original);
            if (!string.IsNullOrWhiteSpace(entry.English)) text.Append("English: ").AppendLine(entry.English);
            if (!string.IsNullOrWhiteSpace(entry.Warning)) text.Append("Da verificare: ").AppendLine(entry.Warning);
            text.AppendLine();
        }
        return text.ToString();
    }
}

public static class MeetingText
{
    public static string RemoveOverlap(string previous, string current, bool overlap)
    {
        current = current.Trim();
        if (!overlap || previous.Length == 0) return current;
        var a = Regex.Matches(previous, @"[\p{L}\p{N}]+(?:['’][\p{L}]+)?");
        var b = Regex.Matches(current, @"[\p{L}\p{N}]+(?:['’][\p{L}]+)?");
        for (int count = Math.Min(8, Math.Min(a.Count, b.Count)); count > 0; count--)
        {
            if (!Enumerable.Range(0, count).All(i => string.Equals(a[a.Count - count + i].Value, b[i].Value, StringComparison.OrdinalIgnoreCase))) continue;
            return current[(b[count - 1].Index + b[count - 1].Length)..].TrimStart(' ', ',', '.', ';', ':', '!', '?');
        }
        return current;
    }
}
