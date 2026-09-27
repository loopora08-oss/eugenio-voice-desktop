using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using VoceLocale;
using VoceLocale.Core;

internal static class MeetingSoak
{
    public static async Task<int> RunAsync(string engineRoot, string fixture, string reportPath)
    {
        byte[] pcm = ReadPcm(fixture);
        using var engine = new EngineClient("WhisperWorker", Path.GetFullPath(engineRoot), realtime: true);
        using var segments = new MeetingAudioSegmenter();
        var time = Stopwatch.StartNew();
        var emittedAt = new ConcurrentDictionary<long, double>();
        var latency = new List<double>();
        int chunks = 0, captions = 0, failures = 0, peakQueue = 0;
        long peakMemory = 0, peakWorkerMemory = 0;
        using var stream = new MeetingStream((chunk, token) => engine.RunAsync(new(Guid.NewGuid().ToString(), "transcribe",
            Storage.ModelPath(Models.FastWhisper), "auto", chunk.Audio, Mode: "fast"), token), (text, token) => Task.FromResult<string?>(text));
        stream.EntryAdded += entry =>
        {
            Interlocked.Increment(ref captions);
            if (emittedAt.TryRemove(entry.Sequence, out double at)) lock (latency) latency.Add(time.Elapsed.TotalSeconds - at);
            if (entry.Warning != null) Interlocked.Increment(ref failures);
        };
        stream.Fault += _ => Interlocked.Increment(ref failures);
        segments.ChunkReady += chunk =>
        {
            Interlocked.Increment(ref chunks); emittedAt[chunk.Sequence] = time.Elapsed.TotalSeconds;
            if (!stream.Submit(chunk, false)) Interlocked.Increment(ref failures);
            peakQueue = Math.Max(peakQueue, stream.PendingChunks);
        };
        var loaded = await engine.RunAsync(new(Guid.NewGuid().ToString(), "load", Storage.ModelPath(Models.FastWhisper)), CancellationToken.None);
        if (!loaded.Ok) throw new IOException("Soak model did not load.");
        time.Restart();
        Console.WriteLine("SOAK_STARTED: 61 minutes of synthetic realtime audio; no microphone or content logs.");
        int offset = 0;
        for (int frame = 0; frame < 61 * 60 * 20; frame++)
        {
            if (offset + 1600 > pcm.Length) offset = 0;
            segments.Push(pcm.AsSpan(offset, 1600)); offset += 1600;
            if (failures > 0) break;
            var delay = TimeSpan.FromSeconds((frame + 1) * .05 - time.Elapsed.TotalSeconds);
            if (delay > TimeSpan.Zero) await Task.Delay(delay);
            if ((frame + 1) % 1200 == 0)
            {
                long memory = Process.GetCurrentProcess().WorkingSet64; peakMemory = Math.Max(peakMemory, memory);
                var worker = engine.Metrics; peakWorkerMemory = Math.Max(peakWorkerMemory, worker.MemoryBytes);
                var progress = new { success = (bool?)null, running = true, minute = (frame + 1) / 1200, capturedSeconds = segments.CapturedSamples / 16000d,
                    chunks, captions, pending = stream.PendingChunks, peakQueue, memoryMb = memory / 1048576, workerMemoryMb = worker.MemoryBytes / 1048576,
                    workerCpuSeconds = worker.ProcessorSeconds, failures, synthetic = true, audioSaved = false, transcriptSaved = false };
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
                File.WriteAllText(reportPath, JsonSerializer.Serialize(progress, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine(JsonSerializer.Serialize(progress));
            }
        }
        segments.Flush(); await stream.CompleteAsync(); Array.Clear(pcm);
        double[] lags; lock (latency) lags = latency.Order().ToArray();
        double p95 = lags.Length == 0 ? double.PositiveInfinity : lags[(int)Math.Floor((lags.Length - 1) * .95)];
        bool success = segments.CapturedSamples >= 61L * 60 * 16000 && time.Elapsed.TotalSeconds >= 3660
            && failures == 0 && captions > 500 && peakQueue < 24 && p95 < 10;
        var report = new { success, elapsedSeconds = time.Elapsed.TotalSeconds, capturedSeconds = segments.CapturedSamples / 16000d,
            chunks, captions, failures, peakQueue, captionP95Seconds = p95, peakHarnessMemoryMb = peakMemory / 1048576, peakWorkerMemoryMb = peakWorkerMemory / 1048576,
            workerCpuSeconds = engine.Metrics.ProcessorSeconds, logicalProcessors = Environment.ProcessorCount, synthetic = true, audioSaved = false, transcriptSaved = false };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(report));
        return success ? 0 : 1;
    }
    private static byte[] ReadPcm(string file)
    {
        using var reader = new BinaryReader(File.OpenRead(file));
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidDataException();
        reader.ReadInt32(); reader.ReadBytes(4); bool correct = false;
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            string chunk = Encoding.ASCII.GetString(reader.ReadBytes(4)); int size = reader.ReadInt32();
            if (chunk == "fmt ")
            {
                byte[] f = reader.ReadBytes(size);
                correct = BitConverter.ToInt16(f, 0) == 1 && BitConverter.ToInt16(f, 2) == 1 && BitConverter.ToInt32(f, 4) == 16000 && BitConverter.ToInt16(f, 14) == 16;
            }
            else if (chunk == "data") { if (!correct) throw new InvalidDataException("Expected synthetic mono PCM16 at 16kHz."); return reader.ReadBytes(size); }
            else reader.BaseStream.Seek(size, SeekOrigin.Current);
            if ((size & 1) != 0) reader.BaseStream.Seek(1, SeekOrigin.Current);
        }
        throw new InvalidDataException();
    }
}
