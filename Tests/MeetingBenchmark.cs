using System.Diagnostics;
using System.Text.Json;
using VoceLocale;
using VoceLocale.Core;

internal static class MeetingBenchmark
{
    // Only local, explicitly synthetic fixtures; the report omits all recognized content.
    public static async Task<int> RunAsync(string engineRoot, string fixtures, string reportPath)
    {
        using var engine = new EngineClient("WhisperWorker", Path.GetFullPath(engineRoot), realtime: true);
        using var text = new OllamaTextService();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var loaded = await engine.RunAsync(new(Guid.NewGuid().ToString(), "load", Storage.ModelPath(Models.FastWhisper)), cancel.Token);
        if (!loaded.Ok) return 1;
        var measurements = new List<object>(); int failures = 0;
        foreach (int seconds in new[] { 10, 30, 120 })
        {
            byte[] pcm = MeetingAudioChecks.ReadPcm(Path.Combine(fixtures, $"benchmark-{seconds}.wav"));
            var entries = new List<MeetingEntry>(); var timings = new List<long>();
            using var segments = new MeetingAudioSegmenter();
            using var stream = new MeetingStream(async (chunk, token) =>
            {
                var result = await engine.RunAsync(new(Guid.NewGuid().ToString(), "transcribe", Storage.ModelPath(Models.FastWhisper), "auto", chunk.Audio, Mode: "fast"), token);
                timings.Add(result.Milliseconds); return result;
            }, (_, _) => Task.FromResult<string?>(null));
            stream.EntryAdded += entry => entries.Add(entry);
            int errors = 0, chunks = 0;
            stream.Fault += _ => errors++;
            segments.ChunkReady += chunk => { chunks++; if (!stream.Submit(chunk, false)) errors++; };
            var watch = Stopwatch.StartNew();
            try
            {
                segments.Push(pcm); segments.Flush(); await stream.CompleteAsync();
                long transcriptionMs = watch.ElapsedMilliseconds;
                string original = string.Join("\n", entries.Select(e => e.Original));
                var notes = await text.NotesAsync("minicpm-assistente-it:latest", original, cancel.Token);
                bool ok = errors == 0 && entries.Count > 0 && entries.All(e => e.Warning == null) && notes.Ok;
                if (!ok) failures++;
                var row = new { synthetic = true, audioSeconds = seconds, chunks, captions = entries.Count,
                    transcriptionMs, summedEngineMs = timings.Sum(), notesMs = notes.Milliseconds,
                    notesExtractiveFallback = notes.ExtractiveFallback, processingCompleted = ok,
                    wordAccuracyMeasured = false, transcriptSaved = false, audioSaved = false };
                measurements.Add(row); Console.WriteLine(JsonSerializer.Serialize(row));
            }
            finally { Array.Clear(pcm); }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { success = failures == 0, measurements }, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }
}
