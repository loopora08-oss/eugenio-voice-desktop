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
            // Offline throughput: do not simulate a 120-second recording as one instant
            // burst into the live queue. The separate soak checks actual live pacing.
            var blocks = new List<MeetingAudioChunk>();
            segments.ChunkReady += blocks.Add;
            int errors = 0;
            var watch = Stopwatch.StartNew();
            try
            {
                segments.Push(pcm); segments.Flush(); string previous = "";
                foreach (var chunk in blocks)
                {
                    var result = await engine.RunAsync(new(Guid.NewGuid().ToString(), "transcribe", Storage.ModelPath(Models.FastWhisper), "auto", chunk.Audio, Mode: "fast"), cancel.Token);
                    timings.Add(result.Milliseconds);
                    if (!result.Ok) { errors++; continue; }
                    string caption = MeetingText.RemoveOverlap(previous, result.Text ?? "", chunk.Overlap);
                    previous = result.Text ?? "";
                    if (caption.Length > 0) entries.Add(new(chunk.Sequence, chunk.StartSeconds, chunk.EndSeconds, caption));
                    Array.Clear(chunk.Audio);
                }
                long transcriptionMs = watch.ElapsedMilliseconds;
                string original = string.Join("\n", entries.Select(e => e.Original));
                var notes = await text.NotesAsync("minicpm-assistente-it:latest", original, cancel.Token);
                bool ok = errors == 0 && entries.Count > 0 && entries.All(e => e.Warning == null) && notes.Ok;
                if (!ok) failures++;
                var row = new { synthetic = true, audioSeconds = seconds, chunks = blocks.Count, captions = entries.Count,
                    transcriptionMs, summedEngineMs = timings.Sum(), notesMs = notes.Milliseconds,
                    notesExtractiveFallback = notes.ExtractiveFallback, processingCompleted = ok,
                    wordAccuracyMeasured = false, transcriptSaved = false, audioSaved = false };
                measurements.Add(row); Console.WriteLine(JsonSerializer.Serialize(row));
            }
            finally { Array.Clear(pcm); foreach (var chunk in blocks) Array.Clear(chunk.Audio); }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { success = failures == 0, measurements }, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }
}
