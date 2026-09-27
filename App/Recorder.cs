using System.IO;
using NAudio.Wave;
using VoceLocale.Core;

namespace VoceLocale;

public sealed class Recorder : IDisposable
{
    private WaveInEvent? _input;
    private MemoryStream? _buffer;
    private MemoryStream? _segment;
    private TaskCompletionSource<byte[]>? _finished;
    private readonly object _lock = new();
    private bool _stopping;
    private bool _discard;
    private bool _segmentSpeaking;
    private bool _voiceVisible;
    private int _silenceBytes;
    private int _generation;
    private const int LimitBytes = 16000 * 2 * 120;
    private const int SilenceLimit = 16000 * 2 * 800 / 1000;
    private const int SegmentMinBytes = 3200;
    public event Action<byte[]>? SegmentReady;
    public event Action<bool>? VoiceActivityChanged;
    public event Action<double>? InputLevelChanged;
    public event Action? LimitReached;
    public event Action? DeviceFailed;
    public static string[] Microphones() => Enumerable.Range(0, WaveIn.DeviceCount)
        .Select(i => WaveIn.GetCapabilities(i).ProductName).ToArray();
    public void Start(int device, ReadOnlyMemory<byte> preRoll = default)
    {
        _discard = false;
        _stopping = false;
        _buffer = new MemoryStream(LimitBytes);
        _segment = new MemoryStream(); _segmentSpeaking = _voiceVisible = false; _silenceBytes = 0;
        if (!preRoll.IsEmpty)
        {
            var initial = preRoll.Span[..Math.Min(preRoll.Length, LimitBytes)];
            _buffer.Write(initial);
            if (TextPolicy.HasVoice(initial) && _segment != null)
            {
                _segment.Write(initial);
                _segmentSpeaking = true;
            }
        }
        _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = _finished;
        int generation = ++_generation;
        _input = new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 50 };
        _input.DataAvailable += (_, e) =>
        {
            InputLevelChanged?.Invoke(AudioSignal.Rms(e.Buffer.AsSpan(0, e.BytesRecorded)));
            bool limit = false;
            bool? voiceChanged = null;
            lock (_lock)
            {
                if (generation != _generation || _stopping || _buffer == null) { Array.Clear(e.Buffer); return; }
                int remaining = LimitBytes - (int)_buffer.Length;
                _buffer.Write(e.Buffer, 0, Math.Min(e.BytesRecorded, remaining));
                int bytes = Math.Min(e.BytesRecorded, remaining) & ~1;
                bool active = AudioSignal.IsActive(e.Buffer.AsSpan(0, bytes));
                if (active) { _segmentSpeaking = true; _silenceBytes = 0; }
                else if (_segmentSpeaking) _silenceBytes += bytes;
                if (active != _voiceVisible && (active || _silenceBytes >= 16000 * 2 / 4))
                {
                    _voiceVisible = active;
                    voiceChanged = active;
                }
                if (_segmentSpeaking && _segment != null) _segment.Write(e.Buffer, 0, bytes);
                if (_segmentSpeaking && _silenceBytes >= SilenceLimit) CommitSegment(trimTail: true);
                Array.Clear(e.Buffer);
                if (_buffer.Length >= LimitBytes) { _stopping = true; limit = true; }
            }
            if (voiceChanged is { } detected) VoiceActivityChanged?.Invoke(detected);
            if (limit) LimitReached?.Invoke();
        };
        _input.RecordingStopped += (_, e) =>
        {
            lock (_lock)
            {
                if (generation != _generation) { completion.TrySetResult([]); return; }
                if (e.Exception != null) { completion.TrySetException(new IOException("Microfono scollegato o non disponibile.")); DeviceFailed?.Invoke(); }
                else
                {
                    if (!_discard) CommitSegment(trimTail: false);
                    completion.TrySetResult(_discard ? [] : _buffer?.ToArray() ?? []);
                }
                Wipe();
            }
        };
        try { _input.StartRecording(); }
        catch { Dispose(); throw; }
    }
    public async Task<byte[]> StopAsync()
    {
        lock (_lock) _stopping = true;
        var input = _input;
        var finished = _finished;
        input?.StopRecording();
        var data = await (finished?.Task ?? Task.FromResult(Array.Empty<byte>()));
        input?.Dispose();
        if (ReferenceEquals(_input, input)) _input = null;
        return data;
    }
    private void CommitSegment(bool trimTail)
    {
        if (_segment == null) return;
        int length = checked((int)_segment.Length);
        if (trimTail) length = Math.Max(0, length - 16000 * 2 * 250 / 1000);
        length &= ~1;
        if (length >= SegmentMinBytes)
        {
            byte[] data = _segment.GetBuffer().AsSpan(0, length).ToArray();
            if (SegmentReady is { } ready) ready(data);
            else Array.Clear(data);
        }
        Array.Clear(_segment.GetBuffer()); _segment.SetLength(0);
        _segmentSpeaking = false; _silenceBytes = 0;
    }
    private void Wipe()
    {
        if (_buffer != null) { Array.Clear(_buffer.GetBuffer()); _buffer.Dispose(); _buffer = null; }
        if (_segment != null) { Array.Clear(_segment.GetBuffer()); _segment.Dispose(); _segment = null; }
        _voiceVisible = false;
    }
    public void Dispose()
    {
        lock (_lock) { _discard = true; _stopping = true; _generation++; _finished?.TrySetResult([]); }
        _input?.StopRecording();
        _input?.Dispose();
        _input = null;
        lock (_lock) Wipe();
    }
}
