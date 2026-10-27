using NAudio.Wave;
using System.Diagnostics;
using System.IO;
using VoceLocale.Core;

namespace VoceLocale;

public sealed class MeetingRecorder : IDisposable
{
    private sealed record Capture(IWaveIn Input, TaskCompletionSource Stopped);
    private readonly object _gate = new();
    private readonly MeetingSourceTimeline _timeline;
    private readonly List<Capture> _captures = new();
    private readonly Stopwatch _clock = new();
    private readonly bool _includeSystemAudio;
    private System.Threading.Timer? _silenceTimer;
    private Task? _stopTask;
    private bool _stopping, _disposed, _starting;
    private readonly Func<int, IWaveIn> _microphoneFactory;
    private readonly Func<IWaveIn> _systemFactory;
    public event Action<MeetingAudioChunk>? ChunkReady;
    public event Action<double>? InputLevelChanged;
    public event Action? DeviceFailed;
    public double CapturedSeconds => _clock.Elapsed.TotalSeconds;

    public MeetingRecorder(bool includeSystemAudio = false, Func<int, IWaveIn>? microphoneFactory = null,
        Func<IWaveIn>? systemFactory = null)
    {
        _includeSystemAudio = includeSystemAudio;
        _microphoneFactory = microphoneFactory ?? (device => new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 50 });
        _systemFactory = systemFactory ?? (() => new WasapiLoopbackCapture { WaveFormat = new WaveFormat(16000, 16, 1) });
        _timeline = new(includeSystemAudio);
        _timeline.ChunkReady += chunk => { if (ChunkReady is { } ready) ready(chunk); else Array.Clear(chunk.Audio); };
    }
    public void Start(int device)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_captures.Count > 0 || _starting) throw new InvalidOperationException("Acquisizione già attiva.");
            _starting = true;
        }
        try
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _stopping = false; _stopTask = null;
                if (!_clock.IsRunning) _clock.Start();
                var microphone = _microphoneFactory(device);
                Attach(microphone, MeetingAudioSource.Microphone);
                if (_includeSystemAudio)
                {
                    // WASAPI condiviso ricampiona tramite AutoConvertPcm/SrcDefaultQuality di NAudio.
                    var system = _systemFactory();
                    Attach(system, MeetingAudioSource.System);
                }
                foreach (var capture in _captures) capture.Input.StartRecording();
                _silenceTimer = new(_ => AdvanceSilence(), null, 100, 100);
            }
        }
        catch { Dispose(); throw; }
        finally { lock (_gate) _starting = false; }
    }
    private void Attach(IWaveIn input, MeetingAudioSource source)
    {
        if (input.WaveFormat.Encoding != WaveFormatEncoding.Pcm || input.WaveFormat.SampleRate != 16000
            || input.WaveFormat.BitsPerSample != 16 || input.WaveFormat.Channels != 1)
        {
            input.Dispose();
            throw new InvalidDataException("Formato audio non supportato: serve PCM16 mono a 16 kHz.");
        }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var capture = new Capture(input, completion);
        input.DataAvailable += (_, e) =>
        {
            bool failed = false;
            try
            {
                lock (_gate)
                {
                    if (_disposed || _stopping || !_captures.Contains(capture)) return;
                    if (source == MeetingAudioSource.Microphone) InputLevelChanged?.Invoke(AudioSignal.Rms(e.Buffer.AsSpan(0, e.BytesRecorded)));
                    _timeline.Push(source, e.Buffer.AsSpan(0, e.BytesRecorded), _clock.Elapsed);
                }
            }
            catch { failed = true; }
            finally { Array.Clear(e.Buffer); }
            if (failed) DeviceFailed?.Invoke();
        };
        input.RecordingStopped += (_, _) =>
        {
            bool unexpected;
            lock (_gate) unexpected = !_disposed && !_stopping && _captures.Contains(capture);
            completion.TrySetResult();
            if (unexpected) DeviceFailed?.Invoke();
        };
        _captures.Add(capture);
    }
    private void AdvanceSilence()
    {
        bool failed = false;
        try
        {
            lock (_gate)
            {
                if (_disposed || _stopping || _captures.Count == 0) return;
                // Il loopback può non emettere frame quando l'uscita è silenziosa.
                if (_clock.Elapsed > TimeSpan.FromMilliseconds(250))
                    _timeline.AdvanceTo(_clock.Elapsed - TimeSpan.FromMilliseconds(250));
            }
        }
        catch { failed = true; }
        if (failed) DeviceFailed?.Invoke();
    }
    public Task StopAsync()
    {
        lock (_gate) return _stopTask ??= StopCoreAsync();
    }
    private async Task StopCoreAsync()
    {
        Capture[] captures;
        lock (_gate)
        {
            _stopping = true; _silenceTimer?.Dispose(); _silenceTimer = null;
            captures = _captures.ToArray();
        }
        try
        {
            foreach (var capture in captures) capture.Input.StopRecording();
            await Task.WhenAll(captures.Select(c => c.Stopped.Task)).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        finally
        {
            foreach (var capture in captures) capture.Input.Dispose();
            lock (_gate)
            {
                _captures.Clear();
                if (!_disposed) { _timeline.AdvanceTo(_clock.Elapsed); _timeline.Flush(); }
            }
        }
    }
    public void Dispose()
    {
        Capture[] captures;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _stopping = true;
            _silenceTimer?.Dispose(); _silenceTimer = null;
            captures = _captures.ToArray(); _captures.Clear(); _clock.Stop();
        }
        // Fuori dal lock: Dispose WASAPI attende il thread che può completare un callback.
        foreach (var capture in captures) { try { capture.Input.StopRecording(); } catch { } capture.Input.Dispose(); }
        _timeline.Dispose();
    }
}
