using System.Diagnostics;
using System.Text;
using System.Text.Json;
using VoceLocale.Core;
using Whisper.net;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);
WhisperFactory? factory = null;
string loaded = "";
while (await Console.In.ReadLineAsync() is { } line)
{
    EngineRequest? request = null;
    try
    {
        request = JsonSerializer.Deserialize<EngineRequest>(line) ?? throw new InvalidDataException();
        var watch = Stopwatch.StartNew();
        if (loaded != request.ModelPath)
        {
            factory?.Dispose();
            factory = WhisperFactory.FromPath(request.ModelPath);
            loaded = request.ModelPath;
        }
        string result = "";
        if (request.Operation == "transcribe")
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
                    using var processor = factory!.CreateBuilder().WithLanguage(request.Language)
                        .WithThreads(Math.Clamp(Environment.ProcessorCount / 2, 2, 8)).Build();
                    var sb = new StringBuilder();
                    await foreach (var segment in processor.ProcessAsync(samples)) sb.Append(segment.Text).Append(' ');
                    result = sb.ToString().Trim();
                    sb.Clear();
                }
                finally { Array.Clear(samples); }
            }
            finally { Array.Clear(pcm); }
        }
        else if (request.Operation != "load") throw new InvalidDataException();
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new EngineResponse(request.Id, true, result, Milliseconds: watch.ElapsedMilliseconds)));
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
factory?.Dispose();
