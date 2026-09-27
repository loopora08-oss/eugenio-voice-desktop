using System.IO;
using NAudio.Wave;
using VoceLocale.Core;

namespace VoceLocale;

/// <summary>Capture remains active during inference; overlapping windows are queued in memory.</summary>
public sealed class VoiceWakeListener : IDisposable
{
    private sealed record Segment(byte[] Audio, long End);
    private readonly object _gate = new();
    private WaveInEvent? _input;
    private MemoryStream? _phrase;
    private readonly WakeAudioHistory _history = new();
    private readonly Queue<Segment> _waiting = new();
    private Func<byte[], CancellationToken, Task>? _recognize;
    private CancellationTokenSource? _recognitionSession;
    private readonly AudioLeadIn _leadIn = new(16000 * 2 / 2);
    private int _silenceBytes, _generation;
    private long _recognizingEnd;
    private bool _speaking, _processing, _disposed, _voiceVisible;
    private const int SilenceLimit = 16000 * 2 * 4 / 10;
    private const int PhraseLimit = 16000 * 2 * 2;
    public event Action? Failed;
    public event Action<bool>? VoiceActivityChanged;
    public event Action<double>? InputLevelChanged;

    public void Start(int device, Func<byte[], CancellationToken, Task> recognize)
    {
        Stop();
        lock (_gate)
        {
            _disposed = false; _recognize = recognize;
            _recognitionSession = new CancellationTokenSource();
            _leadIn.Clear(); _history.Clear(); _recognizingEnd = 0;
            _phrase = new MemoryStream(PhraseLimit); _silenceBytes = 0;
            _speaking = _processing = _voiceVisible = false;
            _input = new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 50 };
            _input.DataAvailable += OnData;
            _input.RecordingStopped += (_, e) => { if (e.Exception != null) Failed?.Invoke(); };
            try { _input.StartRecording(); }
            catch { _input.Dispose(); _input = null; throw; }
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        InputLevelChanged?.Invoke(AudioSignal.Rms(e.Buffer.AsSpan(0, e.BytesRecorded)));
        Segment? run = null;
        int generation = 0;
        bool? voiceChanged = null;
        lock (_gate)
        {
            if (_disposed || _phrase == null) { Array.Clear(e.Buffer); return; }
            int bytes = e.BytesRecorded & ~1;
            if (bytes == 0) { Array.Clear(e.Buffer); return; }
            _history.Append(e.Buffer.AsSpan(0, bytes));
            bool active = AudioSignal.IsActive(e.Buffer.AsSpan(0, bytes));
            if (active)
            {
                if (!_speaking) _leadIn.DrainTo(_phrase);
                _speaking = true; _silenceBytes = 0;
            }
            else if (_speaking) _silenceBytes += bytes;
            else _leadIn.Push(e.Buffer.AsSpan(0, bytes));
            if (active != _voiceVisible && (active || _silenceBytes >= 16000 * 2 / 5))
            { _voiceVisible = active; voiceChanged = active; }
            if (_speaking) _phrase.Write(e.Buffer, 0, bytes);
            Array.Clear(e.Buffer);
            bool full = _phrase.Length >= PhraseLimit;
            if (full || (_speaking && _silenceBytes >= SilenceLimit))
            {
                var segment = new Segment(_phrase.ToArray(), _history.Position);
                // Overlap protects Eugenio if the name crosses a window boundary.
                if (full) _leadIn.Push(segment.Audio.AsSpan(Math.Max(0, segment.Audio.Length - 16000)));
                Array.Clear(_phrase.GetBuffer()); _phrase.SetLength(0);
                _silenceBytes = 0; _speaking = false;
                if (_processing)
                {
                    // Keep the freshest bounded backlog under sustained ambient speech.
                    if (_waiting.Count == 4) Array.Clear(_waiting.Dequeue().Audio);
                    _waiting.Enqueue(segment);
                }
                else { _processing = true; run = segment; generation = _generation; }
            }
        }
        if (voiceChanged is { } detected) VoiceActivityChanged?.Invoke(detected);
        if (run != null) _ = ProcessAsync(run, generation);
    }

    private async Task ProcessAsync(Segment segment, int generation)
    {
        try
        {
            Func<byte[], CancellationToken, Task>? recognize;
            CancellationToken token;
            lock (_gate)
            {
                recognize = generation == _generation ? _recognize : null;
                token = _recognitionSession?.Token ?? new CancellationToken(true);
                if (recognize != null) _recognizingEnd = segment.End;
            }
            if (recognize != null) await recognize(segment.Audio, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch
        {
            bool current;
            lock (_gate) current = generation == _generation && !_disposed;
            if (current) Failed?.Invoke();
        }
        finally
        {
            Array.Clear(segment.Audio);
            Segment? next = null;
            lock (_gate)
            {
                if (generation == _generation && !_disposed)
                {
                    if (_waiting.Count > 0) next = _waiting.Dequeue();
                    _processing = next != null;
                }
            }
            if (next != null) _ = ProcessAsync(next, generation);
        }
    }

    public byte[] StopAndTakeProcessingAudio() => StopCore(true);
    public void Stop() => StopCore(false);
    private byte[] StopCore(bool takeAudio)
    {
        WaveInEvent? input;
        byte[] audio = [];
        CancellationTokenSource? session;
        lock (_gate)
        {
            _disposed = true; _recognize = null;
            session = _recognitionSession; _recognitionSession = null;
            _generation++;
            input = _input; _input = null;
            if (takeAudio) audio = _history.After(_recognizingEnd);
            _history.Clear(); _leadIn.Clear();
            while (_waiting.Count > 0) Array.Clear(_waiting.Dequeue().Audio);
            if (_phrase != null) { Array.Clear(_phrase.GetBuffer()); _phrase.Dispose(); _phrase = null; }
            if (_voiceVisible) VoiceActivityChanged?.Invoke(false);
            _voiceVisible = _speaking = _processing = false; _silenceBytes = 0;
        }
        session?.Cancel(); session?.Dispose();
        if (input != null) { try { input.StopRecording(); } catch { } input.Dispose(); }
        return audio;
    }
    public void Dispose() => Stop();
}
