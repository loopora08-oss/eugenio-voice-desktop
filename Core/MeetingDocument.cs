using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace VoceLocale.Core;

public sealed record MeetingEntry(long Sequence, double StartSeconds, double EndSeconds,
    string Original, string? English = null, string? Warning = null, bool TranslationRequested = false, string? Language = null,
    MeetingAudioSource Source = MeetingAudioSource.Microphone, RecognitionProvenance? Provenance = null);

public sealed class MeetingDocument
{
    public int FormatVersion { get; set; } = 1;
    public string Title { get; set; } = "Meeting Eugenio";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public double DurationSeconds { get; set; }
    public List<MeetingEntry> Entries { get; set; } = [];
    public string Notes { get; set; } = "";
    public int? NotedEntries { get; set; }
    // Calcolati dal contenuto: metadati in ingresso non possono sovrascrivere questa dichiarazione.
    public MeetingAiMetadata AiMetadata => new(1, "Eugenio", "ai-assisted-workflow", true,
        Entries == null || !Entries.Any(e => e != null && !string.IsNullOrWhiteSpace(e.Original)) ? "unavailable" :
        Entries.Where(e => e != null && !string.IsNullOrWhiteSpace(e.Original)).All(e => e.Provenance?.IsValid() == true) ? "recorded" :
        Entries.Any(e => e?.Provenance?.IsValid() == true) ? "partial" : "unavailable",
        "not-recorded", "self-reported-not-signed");

    public bool IsValid()
    {
        // Also bound TimeSpan conversions before replacing an open document.
        const double maximumSeconds = 36525d * 24 * 60 * 60;
        return FormatVersion == 1 && Title != null && Notes != null && Entries != null && Entries.Count <= 100000
            && (NotedEntries == null || NotedEntries >= 0 && NotedEntries <= Entries.Count)
            && double.IsFinite(DurationSeconds) && DurationSeconds >= 0 && DurationSeconds <= maximumSeconds
            && Entries.All(e => e != null && e.Original != null && double.IsFinite(e.StartSeconds) && double.IsFinite(e.EndSeconds)
                && e.StartSeconds >= 0 && e.EndSeconds >= e.StartSeconds && e.EndSeconds <= maximumSeconds
                && Enum.IsDefined(e.Source) && (e.Provenance == null || e.Provenance.IsValid()));
    }

    public string Json()
    {
        if (!IsValid()) throw new InvalidDataException("Documento non valido per l'esportazione.");
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    public string Markdown()
    {
        if (!IsValid()) throw new InvalidDataException("Documento non valido per l'esportazione.");
        var text = new StringBuilder().Append("<!-- eugenio-ai-metadata: ").Append(JsonSerializer.Serialize(AiMetadata)).AppendLine(" -->")
            .AppendLine("Contenuto elaborato con assistenza IA locale. Verifica trascrizione, traduzioni e appunti prima di condividerli.").AppendLine()
            .Append("# ").AppendLine(Title).AppendLine()
            .Append("Data: ").AppendLine(CreatedAt.ToString("yyyy-MM-dd HH:mm zzz"))
            .Append("Durata: ").AppendLine(TimeSpan.FromSeconds(DurationSeconds).ToString(@"hh\:mm\:ss"))
            .AppendLine().AppendLine("## Appunti").AppendLine().AppendLine(Notes)
            .AppendLine().AppendLine("## Trascrizione").AppendLine();
        foreach (var entry in Entries)
        {
            text.Append("[").Append(TimeSpan.FromSeconds(entry.StartSeconds).ToString(@"hh\:mm\:ss"))
                .Append("] ").Append(entry.Source == MeetingAudioSource.System ? "Audio PC · parlante non identificato: " : "Microfono: ").AppendLine(entry.Original);
            if (!string.IsNullOrWhiteSpace(entry.English)) text.Append("English: ").AppendLine(entry.English);
            if (!string.IsNullOrWhiteSpace(entry.Warning)) text.Append("Da verificare: ").AppendLine(entry.Warning);
            if (entry.Provenance != null)
                text.Append("<!-- eugenio-asr-provenance: ").Append(JsonSerializer.Serialize(entry.Provenance)).AppendLine(" -->");
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
