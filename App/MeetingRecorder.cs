using NAudio.Wave;
using System.Diagnostics;
using VoceLocale.Core;

namespace VoceLocale;

public sealed class MeetingRecorder : IDisposable
{
    private readonly object _gate = new();
    private readonly MeetingAudioSegmenter _segments = new();
    private WaveInEvent? _input;
    private TaskCompletionSource? _stopped;
    private bool _stopping, _disposed;
    private readonly Stopwatch _pause = new();
    public event Action<MeetingAudioChunk>? ChunkReady;
    public event Action<double>? InputLevelChanged;
    public event Action? DeviceFailed;
    public double CapturedSeconds => _segments.CapturedSamples / 16000d;
    public MeetingRecorder() => _segments.ChunkReady += chunk => { if (ChunkReady is { } ready) ready(chunk); else Array.Clear(chunk.Audio); };
    public void Start(int device)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_input != null) throw new InvalidOperationException("Capture is already active.");
            if (_pause.IsRunning) { _segments.SkipSilence(_pause.Elapsed); _pause.Reset(); }
            _stopping = false; _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var completion = _stopped;
            var input = new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 50 };
            input.DataAvailable += (_, e) =>
            {
                try
                {
                    lock (_gate)
                    {
                        if (_stopping || _disposed) return;
                        InputLevelChanged?.Invoke(AudioSignal.Rms(e.Buffer.AsSpan(0, e.BytesRecorded)));
                        _segments.Push(e.Buffer.AsSpan(0, e.BytesRecorded));
                    }
                }
                finally { Array.Clear(e.Buffer); }
            };
            input.RecordingStopped += (_, e) =>
            {
                lock (_gate) { if (!_disposed) _segments.Flush(); }
                completion.TrySetResult();
                if (e.Exception != null) DeviceFailed?.Invoke();
            };
            _input = input;
            try { input.StartRecording(); }
            catch { _input = null; input.Dispose(); throw; }
        }
    }
    public async Task StopAsync()
    {
        WaveInEvent? input; Task? stopped;
        lock (_gate) { _stopping = true; input = _input; stopped = _stopped?.Task; }
        if (input == null) return;
        input.StopRecording();
        if (stopped != null) await stopped.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        input.Dispose();
        lock (_gate) { if (ReferenceEquals(_input, input)) { _input = null; _pause.Restart(); } }
    }
    public void Dispose()
    {
        lock (_gate) { _disposed = true; _stopping = true; }
        try { _input?.StopRecording(); } catch { }
        _input?.Dispose(); _input = null;
        lock (_gate) _segments.Dispose();
    }
}
