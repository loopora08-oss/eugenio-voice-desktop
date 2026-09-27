using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using VoceLocale;
using VoceLocale.Core;

internal static class DictationBenchmark
{
    public static async Task<int> RunAsync(string root, string fixtures, string report)
    {
        using var engine = new EngineClient("WhisperWorker", Path.GetFullPath(root), realtime: true);
        using var rewrite = new OllamaRewriter(); using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await engine.RunAsync(new("load", "load", Storage.ModelPath(Models.Whisper)), cancel.Token);
        await engine.RunAsync(new("base-load", "load", Storage.ModelPath(Models.FastWhisper)), cancel.Token);
        await rewrite.RewriteAsync("minicpm-assistente-it:latest", "Buongiorno.", "general", null, cancel.Token);
        var rows = new List<object>(); int failures = 0;
        foreach (var file in new[] { "it-correction.wav", "en-correction.wav", "translation-0.wav", "translation-1.wav", "translation-2.wav" })
        {
            byte[] pcm = MeetingAudioChecks.ReadPcm(Path.Combine(fixtures, file)); var queue = Channel.CreateUnbounded<byte[]>();
            var watch = Stopwatch.StartNew(); int inserts = 0; long asr = 0, edit = 0; bool ok = false;
            var run = SpeechBlockQueue.RunPreparedAsync(queue.Reader, TimeSpan.FromMilliseconds(1500),
                (audio, token) => engine.RunAsync(new(Guid.NewGuid().ToString(), "transcribe", Storage.ModelPath(SpeechProfile.Choose("adaptive", audio.Length, true, true)), "auto", audio, Mode: "fast"), token),
                async (_, recognized, token) =>
                {
                    asr = recognized.Milliseconds;
                    var result = await rewrite.RewriteAsync("minicpm-assistente-it:latest", recognized.Text ?? "", "general", null, token);
                    edit = result.Milliseconds;
                    string text = "";
                    ok = recognized.Ok && result.Ok && TextPolicy.TryValidate(recognized.Text ?? "", result.Text ?? "", false, out text);
                    if (file is "it-correction.wav" or "en-correction.wav") ok &= text.Contains(file.StartsWith("it") ? "giovedì" : "Thursday", StringComparison.OrdinalIgnoreCase) && !text.Contains(file.StartsWith("it") ? "martedì" : "Tuesday", StringComparison.OrdinalIgnoreCase) && text.Contains(file.StartsWith("it") ? "Non voglio annullare" : "do not want to cancel", StringComparison.OrdinalIgnoreCase);
                    if (file == "translation-0.wav") ok &= text.Contains("15") && !text.Contains("50") && text.Contains("Non voglio annullare", StringComparison.OrdinalIgnoreCase);
                    if (file == "translation-1.wav") ok &= text.Contains("Marco") && text.Contains('?') && !text.Contains("quattro", StringComparison.OrdinalIgnoreCase);
                    if (file == "translation-2.wav") ok &= text.Contains("Luca") && text.Contains("Non ho mai", StringComparison.OrdinalIgnoreCase);
                    if (ok) inserts++;
                }, cancel.Token);
            queue.Writer.TryWrite(pcm);
            // Keep the writer open to measure the actual confirmation window, not the stop shortcut.
            await Task.Delay(1700, cancel.Token); queue.Writer.TryComplete(); await run;
            if (!ok || inserts != 1) failures++;
            var row = new { file, passed = ok && inserts == 1, simulatedInsertions = inserts, transcriptionMs = asr, correctionMs = edit, afterAudioBlockMs = watch.ElapsedMilliseconds, audioCleared = pcm.All(x => x == 0) };
            rows.Add(row); Console.WriteLine(JsonSerializer.Serialize(row));
        }
        await File.WriteAllTextAsync(report, JsonSerializer.Serialize(new { success = failures == 0, synthetic = true, microphone = false, includesCapturePause = false, measurements = rows }, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }
}
