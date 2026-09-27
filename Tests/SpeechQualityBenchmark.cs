using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using VoceLocale;
using VoceLocale.Core;

internal static class SpeechQualityBenchmark
{
    private static string[] Words(string text) => Regex.Matches(text.ToLowerInvariant()
        .Replace("cinquanta", "50").Replace("quindici", "15").Replace("due più due", "2 2")
        .Replace("tre", "3").Replace("three", "3").Replace("e-mail", "mail").Replace("email", "mail"), @"[\p{L}\p{N}]+")
        .Select(x => x.Value).ToArray();
    internal static (int Errors, int Words) Distance(string reference, string candidate)
    {
        var expected = Words(reference); var actual = Words(candidate);
        int[] previous = Enumerable.Range(0, actual.Length + 1).ToArray();
        foreach (var word in expected)
        {
            int[] next = new int[actual.Length + 1]; next[0] = previous[0] + 1;
            for (int j = 1; j <= actual.Length; j++) next[j] = Math.Min(Math.Min(next[j - 1] + 1, previous[j] + 1), previous[j - 1] + (word == actual[j - 1] ? 0 : 1));
            previous = next;
        }
        return (previous[^1], expected.Length);
    }
    public static async Task<int> RunAsync(string root, string fixtures, string model, string mode, string report)
    {
        using var engine = new EngineClient("WhisperWorker", Path.GetFullPath(root), realtime: true);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var load = await engine.RunAsync(new("load", "load", model), cancel.Token);
        if (!load.Ok) return 1;
        var cases = new[] {
            ("translation-0.wav", "Sono cinquanta, scusa, quindici euro. Non voglio annullare."),
            ("translation-1.wav", "Scrivi una mail a Marco. Quanto fa due più due?"),
            ("translation-2.wav", "Sei disponibile? Non ho mai visto Luca."),
            ("it-correction.wav", "Ci vediamo martedì, no, intendevo giovedì alle tre. Non voglio annullare la prenotazione."),
            ("en-correction.wav", "Let us meet on Tuesday, no, Thursday at three. I do not want to cancel the reservation.") }.ToList();
        string manifest = Path.Combine(fixtures, "references.json");
        if (File.Exists(manifest)) cases.AddRange(JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(manifest))!.Select(x => (x.Key, x.Value)));
        var results = new List<object>(); int errors = 0, totalWords = 0; long milliseconds = 0;
        foreach (var (file, reference) in cases)
        {
            foreach (bool chunks in new[] { false, true })
            {
                byte[] audio = MeetingAudioChecks.ReadPcm(Path.Combine(fixtures, file));
                var pieces = new List<MeetingAudioChunk>();
                if (chunks) { using var segmenter = new MeetingAudioSegmenter(); segmenter.ChunkReady += pieces.Add; segmenter.Push(audio); segmenter.Flush(); }
                else pieces.Add(new(1, 0, audio.Length / 2, audio.ToArray(), false));
                string previous = ""; string? lastLanguage = null; var captions = new List<string>(); long duration = 0; bool ok = true;
                var wall = Stopwatch.StartNew();
                foreach (var piece in pieces)
                {
                    string selected = mode == "adaptive" ? Storage.ModelPath(SpeechProfile.Choose("adaptive", piece.Audio.Length, true, true, lastLanguage)) : model;
                    var result = await engine.RunAsync(new(Guid.NewGuid().ToString(), "transcribe", selected, "auto", piece.Audio, Mode: mode == "adaptive" ? "fast" : mode), cancel.Token);
                    duration += result.Milliseconds; ok &= result.Ok;
                    if (result.Ok) lastLanguage = result.Language;
                    captions.Add(MeetingText.RemoveOverlap(previous, result.Text ?? "", piece.Overlap)); previous = result.Text ?? "";
                    Array.Clear(piece.Audio);
                }
                string text = string.Join(" ", captions); var score = Distance(reference, text);
                errors += score.Errors; totalWords += score.Words; milliseconds += duration;
                results.Add(new { file, chunks, ok, wordErrors = score.Errors, referenceWords = score.Words, milliseconds = duration, wallMilliseconds = wall.ElapsedMilliseconds, text, reference });
                Console.WriteLine(JsonSerializer.Serialize(new { file, chunks, ok, score.Errors, duration, text }));
                Array.Clear(audio);
            }
        }
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { synthetic = true, mode, model = Path.GetFileName(model), loadMilliseconds = load.Milliseconds, wordErrors = errors, referenceWords = totalWords, normalizedWer = errors / (double)totalWords, milliseconds, results }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
