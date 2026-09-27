using System.Diagnostics;
using System.Text;
using System.Text.Json;
using VoceLocale.Core;
using Whisper.net;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);
WhisperFactory? factory = null;
var factories = new Dictionary<string, WhisperFactory>(StringComparer.OrdinalIgnoreCase);
while (await Console.In.ReadLineAsync() is { } line)
{
    EngineRequest? request = null;
    try
    {
        request = JsonSerializer.Deserialize<EngineRequest>(line) ?? throw new InvalidDataException();
        var watch = Stopwatch.StartNew();
        if (!factories.TryGetValue(request.ModelPath, out factory))
        {
            if (factories.Count == 2)
            {
                var oldest = factories.First(); oldest.Value.Dispose(); factories.Remove(oldest.Key);
            }
            factory = WhisperFactory.FromPath(request.ModelPath);
            factories.Add(request.ModelPath, factory);
        }
        string result = "";
        string? language = null;
        if (request.Operation is "transcribe" or "wake" or "translate")
        {
            var pcm = request.Audio ?? throw new InvalidDataException();
            try
            {
                if (!TextPolicy.HasVoice(pcm)) throw new InvalidOperationException("silence");
                var samples = new float[pcm.Length / 2];
                try
                {
                    for (int i = 0; i < samples.Length; i++) samples[i] = (short)(pcm[2 * i] | pcm[2 * i + 1] << 8) / 32768f;
                    float peak = samples.Select(Math.Abs).DefaultIfEmpty(0).Max();
                    float gain = peak > 0 ? Math.Clamp(.8f / peak, 1, 12) : 1;
                    if (gain > 1) for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
                    var builder = factory!.CreateBuilder().WithLanguage(request.Language).WithNoContext()
                        .WithThreads(Math.Clamp(Environment.ProcessorCount / 2, 2, 8));
                    if (request.Operation == "translate") builder.WithTranslate();
                    if (request.Operation == "wake") builder.WithSingleSegment().WithTemperature(0).WithTemperatureInc(0)
                        .WithPrompt("Eugenio.");
                    if (request.Operation == "wake") builder.WithAudioContextSize(Math.Max(512, TextPolicy.FastAudioContext(samples.Length)));
                    else if (request.Mode == "fast") builder.WithAudioContextSize(TextPolicy.FastAudioContext(samples.Length)).WithTemperature(0).WithTemperatureInc(0);
                    if (request.Operation == "transcribe" && request.Mode == "fast" && samples.Length <= 6 * 16000) builder.WithSingleSegment();
                    if (request.Operation == "transcribe" && samples.Length <= 6 * 16000)
                        builder.WithPrompt("Buongiorno. Come stai? Can you help me? Email, e-mail, mail, Eugenio, ChatGPT, Claude, Codex, Marco, Luca. Lunedì, martedì, mercoledì, giovedì, venerdì, sabato, domenica. " + string.Join(", ", (request.Vocabulary ?? []).Where(x => x.Length <= 60).Take(40)));
                    if (request.Operation == "transcribe" && samples.Length > 6 * 16000 && request.Vocabulary is { Length: > 0 })
                        builder.WithPrompt(string.Join(", ", request.Vocabulary.Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 60).Take(40)));
                    using var processor = builder.Build();
                    var sb = new StringBuilder();
                    await foreach (var segment in processor.ProcessAsync(samples)) { sb.Append(segment.Text).Append(' '); language ??= segment.Language; }
                    // Automatic mode supports Italian and English. Short Italian phrases can
                    // otherwise be misclassified as Portuguese: restrict that fallback only.
                    if (request.Language == "auto" && language is not ("it" or "en"))
                    {
                        var detected = processor.DetectLanguageWithProbability(samples.AsSpan(), ["it", "en"]);
                        language = detected.language ?? "it";
                        processor.ChangeLanguage(language); sb.Clear();
                        await foreach (var segment in processor.ProcessAsync(samples)) sb.Append(segment.Text).Append(' ');
                    }
                    result = sb.ToString().Trim();
                    sb.Clear();
                }
                finally { Array.Clear(samples); }
            }
            finally { Array.Clear(pcm); }
        }
        else if (request.Operation != "load") throw new InvalidDataException();
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new EngineResponse(request.Id, true, result, Milliseconds: watch.ElapsedMilliseconds, Language: language)));
    }
    catch (Exception ex)
    {
        // Never serialize exception messages, which could contain transcript data or file contents.
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new EngineResponse(request?.Id ?? "", false,
            Error: ex.Message == "silence" ? "silence" : "engine")));
    }
    request = null;
    line = "";
}
foreach (var cached in factories.Values) cached.Dispose();
