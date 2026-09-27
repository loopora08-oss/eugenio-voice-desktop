using System.Diagnostics;
using System.Text.Json;
using VoceLocale;
using VoceLocale.Core;

internal static class MeetingChunkChecks
{
    public static async Task<int> RunAsync(string engineRoot, string fixtures)
    {
        using var engine = new EngineClient("WhisperWorker", Path.GetFullPath(engineRoot), realtime: true);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var loaded = await engine.RunAsync(new(Guid.NewGuid().ToString(), "load", Storage.ModelPath(Models.FastWhisper)), cancel.Token);
        if (!loaded.Ok) return 1;
        int failures = 0;
        foreach (string file in new[] { "translation-0.wav", "translation-1.wav", "translation-2.wav", "it-correction.wav", "en-correction.wav", "benchmark-30.wav" })
        {
            byte[] pcm = MeetingAudioChecks.ReadPcm(Path.Combine(fixtures, file));
            using var segmenter = new MeetingAudioSegmenter();
            var entries = new List<MeetingEntry>(); var durations = new List<long>();
            int errors = 0, chunks = 0;
            string? backend = null;
            using var stream = new MeetingStream(async (chunk, token) =>
            {
                var result = await engine.RunAsync(new(Guid.NewGuid().ToString(), "transcribe", Storage.ModelPath(Models.FastWhisper), "auto", chunk.Audio, Mode: "fast"), token);
                durations.Add(result.Milliseconds); backend = result.Backend;
                return result;
            }, (_, _) => Task.FromResult<string?>(null));
            stream.EntryAdded += e => { entries.Add(e); if (e.Warning != null) errors++; };
            stream.Fault += _ => errors++;
            segmenter.ChunkReady += chunk => { chunks++; if (!stream.Submit(chunk, false)) errors++; };
            var watch = Stopwatch.StartNew();
            segmenter.Push(pcm); segmenter.Flush(); await stream.CompleteAsync();
            string source = string.Join(" ", entries.Select(e => e.Original));
            bool faithful = errors == 0 && entries.Count > 0 && entries.Count <= chunks && file switch
            {
                "translation-0.wav" => source.Contains("50") && source.Contains("15") && source.Contains("non", StringComparison.OrdinalIgnoreCase) && source.Contains("annull", StringComparison.OrdinalIgnoreCase),
                "translation-1.wav" => source.Contains("Marco") && source.Contains("mail", StringComparison.OrdinalIgnoreCase) && source.Contains("quanto", StringComparison.OrdinalIgnoreCase) && source.Contains('?'),
                "translation-2.wav" => source.Contains("Luca") && source.Contains("non", StringComparison.OrdinalIgnoreCase) && source.Contains("disponibile", StringComparison.OrdinalIgnoreCase) && source.Contains('?'),
                "it-correction.wav" => source.Contains("martedì", StringComparison.OrdinalIgnoreCase) && source.Contains("giovedì", StringComparison.OrdinalIgnoreCase) && source.Contains("non", StringComparison.OrdinalIgnoreCase),
                "en-correction.wav" => source.Contains("Tuesday", StringComparison.OrdinalIgnoreCase) && source.Contains("Thursday", StringComparison.OrdinalIgnoreCase) && source.Contains("not", StringComparison.OrdinalIgnoreCase),
                _ => source.Length > 100
            };
            if (!faithful) failures++;
            Console.WriteLine(JsonSerializer.Serialize(new { synthetic = true, file, backend, criticalFieldsPreserved = faithful, chunks, source, durationSeconds = pcm.Length / 32000d, processingSeconds = watch.Elapsed.TotalSeconds, chunkMilliseconds = durations }));
            Array.Clear(pcm);
        }
        Console.WriteLine($"MEETING_CHUNKS: {failures} failures"); return failures == 0 ? 0 : 1;
    }
}
