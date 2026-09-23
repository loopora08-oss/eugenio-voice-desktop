using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LLama;
using LLama.Common;
using LLama.Sampling;
using LLama.Native;
using VoceLocale.Core;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(false);
LLamaWeights? weights = null;
ModelParams? parameters = null;
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
            weights?.Dispose();
            parameters = new ModelParams(request.ModelPath)
            {
                ContextSize = 4096, GpuLayerCount = 0,
                Threads = Math.Clamp(Environment.ProcessorCount / 2, 2, 8),
                BatchThreads = Math.Clamp(Environment.ProcessorCount / 2, 2, 8)
            };
            weights = LLamaWeights.LoadFromFile(parameters);
            loaded = request.ModelPath;
        }
        string result = "";
        if (request.Operation == "rewrite")
        {
            string original = request.Text ?? throw new InvalidDataException();
            async Task<string> Edit(string input, bool correction = false)
            {
                var prompt = correction
                    ? TextPolicy.BuildCorrectionPrompt(input, request.Mode, request.Vocabulary)
                    : TextPolicy.BuildPrompt(input, request.Mode, request.Vocabulary);
                if (weights!.Tokenize(prompt, true, true, Encoding.UTF8).Length + 1536 >= 4096)
                    throw new InvalidDataException();
                var executor = new StatelessExecutor(weights!, parameters!);
                var sb = new StringBuilder();
                using var sampler = new CompletionSampler(weights);
                var inference = new InferenceParams
                {
                    MaxTokens = 1536,
                    AntiPrompts = ["<|im_end|>", "<|im_start|>"],
                    SamplingPipeline = sampler
                };
                await foreach (var piece in executor.InferAsync(prompt, inference)) sb.Append(piece);
                if (!TextPolicy.TryValidate(input, sb.ToString(), !sampler.Finished, out var edited))
                    throw new UnsafeRewriteException();
                sb.Clear();
                return edited;
            }
            var sentences = TextPolicy.Sentences(original);
            var prepared = new List<string>();
            if (sentences.Count > 1)
            {
                // Long transcripts can make a small CPU model overlook local corrections.
                // Resolve explicit corrections in their own sentence before the final cleanup.
                // The complete recording is still returned as a single atomic result.
                foreach (var sentence in sentences)
                    prepared.Add(TextPolicy.HasCorrection(sentence) ? await Edit(sentence, true) : sentence);
                result = await Edit(string.Join(" ", prepared));
            }
            else result = await Edit(original, TextPolicy.HasCorrection(original));
        }
        else if (request.Operation != "load") throw new InvalidDataException();
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new EngineResponse(request.Id, true, result, Milliseconds: watch.ElapsedMilliseconds)));
    }
    catch (UnsafeRewriteException)
    {
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new EngineResponse(request?.Id ?? "", false, Error: "unsafe_output")));
    }
    catch
    {
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new EngineResponse(request?.Id ?? "", false, Error: "engine")));
    }
    request = null;
    line = "";
}
weights?.Dispose();

sealed class UnsafeRewriteException : Exception { }

sealed class CompletionSampler(LLamaWeights weights) : ISamplingPipeline
{
    private readonly DefaultSamplingPipeline inner = new() { Temperature = 0.2f, Seed = 42, TopP = 0.8f, TopK = 20 };
    public bool Finished { get; private set; }
    public LLamaToken Sample(SafeLLamaContextHandle context, int index)
    {
        var token = inner.Sample(context, index);
        if (token.IsEndOfGeneration(weights.NativeHandle)) Finished = true;
        return token;
    }
    public void Apply(SafeLLamaContextHandle context, LLamaTokenDataArray data) => inner.Apply(context, data);
    public void Accept(LLamaToken token) => inner.Accept(token);
    public void Reset() { Finished = false; inner.Reset(); }
    public void Dispose() => inner.Dispose();
}
