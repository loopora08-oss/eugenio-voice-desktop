using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VoceLocale.Core;

namespace VoceLocale;

public static class Storage
{
    // Keep user-writable state out of Program Files. IT can optionally stage verified
    // models in ProgramData so users do not each download a multi-gigabyte copy.
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Eugenio");
    public static string ModelPath(ModelSpec model)
    {
        var userModel = Path.Combine(Root, "models", model.FileName);
        if (File.Exists(userModel)) return userModel;

        // Read-only compatibility for earlier portable builds.
        var portableModel = Path.Combine(AppContext.BaseDirectory, "data", "models", model.FileName);
        if (File.Exists(portableModel)) return portableModel;

        var managedModel = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Eugenio", "models", model.FileName);
        return File.Exists(managedModel) ? managedModel : userModel;
    }
    public static Settings LoadSettings()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new(); }
        catch { return new(); }
    }
    public static void SaveSettings(Settings settings)
    {
        Directory.CreateDirectory(Root);
        var file = SettingsPath;
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(settings));
        File.Move(file + ".tmp", file, true);
    }
    public static string SettingsPath => Path.Combine(Root, "settings.json");
    public static async Task<bool> VerifyAsync(ModelSpec spec, string file, CancellationToken token)
    {
        if (!File.Exists(file) || new FileInfo(file).Length != spec.Bytes) return false;
        await using var stream = File.OpenRead(file);
        var hash = await SHA256.HashDataAsync(stream, token);
        return Convert.ToHexString(hash).Equals(spec.Sha256, StringComparison.OrdinalIgnoreCase);
    }
    public static async Task InstallAsync(ModelSpec model, string? import,
        IProgress<(string, double)> progress, CancellationToken token)
    {
        // Downloads/imports always go to the per-user writable store, even when the
        // active model currently comes from the IT-managed ProgramData location.
        var destination = Path.Combine(Root, "models", model.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (import != null && Path.GetFullPath(import).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
        {
            if (!await VerifyAsync(model, destination, token)) throw new InvalidDataException("Modello non valido.");
            return;
        }
        var temporary = destination + ".partial";
        try
        {
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            using var response = import == null ? await client.GetAsync(model.Url, HttpCompletionOption.ResponseHeadersRead, token) : null;
            response?.EnsureSuccessStatusCode();
            await using (var input = import == null ? await response!.Content.ReadAsStreamAsync(token) : File.OpenRead(import))
            await using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 131072, true))
            {
                var buffer = new byte[131072];
                long total = 0;
                int count;
                long lastUpdate = 0;
                while ((count = await input.ReadAsync(buffer, token)) > 0)
                {
                    total += count;
                    if (total > model.Bytes) throw new InvalidDataException("Dimensione modello inattesa.");
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                    if (Environment.TickCount64 - lastUpdate > 150)
                    {
                        progress.Report((model.FileName, 100d * total / model.Bytes));
                        lastUpdate = Environment.TickCount64;
                    }
                }
            }
            progress.Report(("Verifica SHA-256 · " + model.FileName, 100));
            if (!await VerifyAsync(model, temporary, token)) throw new InvalidDataException("Il file non corrisponde al modello previsto (SHA-256).");
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed class EngineClient(string name, string? applicationRoot = null, bool realtime = false) : IDisposable
{
    private Process? _process;
    private readonly SemaphoreSlim _gate = new(1);
    public (long MemoryBytes, double ProcessorSeconds) Metrics
    {
        get
        {
            try { _process?.Refresh(); return _process is { HasExited: false } ? (_process.WorkingSet64, _process.TotalProcessorTime.TotalSeconds) : (0, 0); }
            catch { return (0, 0); }
        }
    }
    public async Task<EngineResponse> RunAsync(EngineRequest request, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            token.ThrowIfCancellationRequested();
            if (_process == null || _process.HasExited)
            {
                _process?.Dispose();
                var path = Path.Combine(applicationRoot ?? AppContext.BaseDirectory, "engines", name, name + ".exe");
                _process = Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
                    WorkingDirectory = Path.GetDirectoryName(path)!
                }) ?? throw new InvalidOperationException("Motore non avviato.");
                if (realtime) { try { _process.PriorityClass = ProcessPriorityClass.AboveNormal; } catch { } }
                // Drain native diagnostics without storing or displaying potentially sensitive output.
                _process.ErrorDataReceived += (_, _) => { };
                _process.BeginErrorReadLine();
            }
            using var cancel = token.Register(Stop);
            await _process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), token);
            await _process.StandardInput.FlushAsync(token);
            while (true)
            {
                var line = await _process.StandardOutput.ReadLineAsync(token);
                if (line == null) throw new IOException("Il motore locale si è arrestato.");
                // Native libraries may print diagnostics. Only accept the correlated JSON response.
                if (!line.StartsWith('{')) continue;
                EngineResponse? response;
                try { response = JsonSerializer.Deserialize<EngineResponse>(line); }
                catch (JsonException) { continue; }
                if (response?.Id == request.Id) return response;
            }
        }
        catch { Stop(); throw; }
        finally { _gate.Release(); }
    }
    public void Stop()
    {
        try { if (_process is { HasExited: false }) _process.Kill(true); } catch { }
    }
    public void Dispose() { Stop(); _process?.Dispose(); }
}
