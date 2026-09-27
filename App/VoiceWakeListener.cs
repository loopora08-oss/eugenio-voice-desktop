using System.IO;
using NAudio.Wave;
using VoceLocale.Core;

namespace VoceLocale;

/// <summary>On-device voice-activity gate. Audio is held only until the wake check completes.</summary>
public sealed class VoiceWakeListener : IDisposable
{
    private readonly object _gate = new();
    private WaveInEvent? _input;
    private MemoryStream? _phrase;
    private MemoryStream? _processingAudio;
    private Func<byte[], CancellationToken, Task>? _recognize;
    private CancellationTokenSource? _recognitionSession;
    private readonly AudioLeadIn _leadIn = new();
    private int _silenceBytes;
    private int _generation;
    private bool _speaking, _processing, _disposed, _voiceVisible;
    private const int SilenceLimit = 16000 * 2 * 7 / 10;
    private const int PhraseLimit = 16000 * 2 * 3;
    private const int ProcessingAudioLimit = 16000 * 2 * 120;
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
            _leadIn.Clear();
            _phrase = new MemoryStream(PhraseLimit); _silenceBytes = 0; _speaking = _processing = _voiceVisible = false;
            _processingAudio = new MemoryStream();
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
        byte[]? segment = null;
        int generation = 0;
        bool? voiceChanged = null;
        lock (_gate)
        {
            if (_disposed || _phrase == null) { Array.Clear(e.Buffer); return; }
            var bytes = e.BytesRecorded & ~1;
            if (bytes == 0) { Array.Clear(e.Buffer); return; }
            if (_processing)
            {
                if (_processingAudio != null)
                {
                    int available = ProcessingAudioLimit - (int)_processingAudio.Length;
                    _processingAudio.Write(e.Buffer, 0, Math.Min(bytes, available));
                }
                Array.Clear(e.Buffer);
                return;
            }
            bool active = AudioSignal.IsActive(e.Buffer.AsSpan(0, bytes));
            if (active)
            {
                if (!_speaking) _leadIn.DrainTo(_phrase);
                _speaking = true; _silenceBytes = 0;
            }
            else if (_speaking) _silenceBytes += bytes;
            else _leadIn.Push(e.Buffer.AsSpan(0, bytes));
            if (active != _voiceVisible && (active || _silenceBytes >= 16000 * 2 / 5))
            {
                _voiceVisible = active;
                voiceChanged = active;
            }
            if (_speaking || active) _phrase.Write(e.Buffer, 0, bytes);
            Array.Clear(e.Buffer);

            if (_phrase.Length >= PhraseLimit || (_speaking && _silenceBytes >= SilenceLimit))
            {
                segment = _phrase.ToArray();
                generation = _generation;
                Array.Clear(_phrase.GetBuffer()); _phrase.SetLength(0);
                _silenceBytes = 0; _speaking = false; _processing = true;
            }
        }
        if (voiceChanged is { } detected) VoiceActivityChanged?.Invoke(detected);
        if (segment != null) _ = ProcessAsync(segment, generation);
    }

    private async Task ProcessAsync(byte[] segment, int generation)
    {
        try
        {
            Func<byte[], CancellationToken, Task>? recognize;
            CancellationToken token;
            lock (_gate)
            {
                recognize = generation == _generation ? _recognize : null;
                token = _recognitionSession?.Token ?? new CancellationToken(true);
            }
            if (recognize != null) await recognize(segment, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch { lock (_gate) { if (generation == _generation && !_disposed) Failed?.Invoke(); } }
        finally
        {
            Array.Clear(segment);
            byte[]? next = null;
            lock (_gate)
            {
                if (generation == _generation && !_disposed)
                {
                    // A wake word spoken during inference must also be checked. Keep only
                    // the recent bounded window, never concatenate it with an older phrase.
                    if (_processingAudio != null)
                    {
                        int length = (int)_processingAudio.Length;
                        var recent = _processingAudio.GetBuffer().AsSpan(Math.Max(0, length - PhraseLimit), Math.Min(length, PhraseLimit));
                        if (TextPolicy.HasVoice(recent)) next = recent.ToArray();
                        Array.Clear(_processingAudio.GetBuffer()); _processingAudio.SetLength(0);
                    }
                    _processing = next != null;
                }
            }
            if (next != null) _ = ProcessAsync(next, generation);
        }
    }

    /// <summary>Stops wake capture and returns audio heard during recognition without a second capture gap.</summary>
    public byte[] StopAndTakeProcessingAudio() => StopCore(takeProcessingAudio: true);

    public void Stop() => StopCore(takeProcessingAudio: false);

    private byte[] StopCore(bool takeProcessingAudio)
    {
        WaveInEvent? input;
        byte[] audio = [];
        CancellationTokenSource? recognitionSession;
        lock (_gate)
        {
            _disposed = true; _recognize = null;
            recognitionSession = _recognitionSession; _recognitionSession = null;
            _leadIn.Clear();
            _generation++;
            input = _input; _input = null;
            if (_phrase != null) { Array.Clear(_phrase.GetBuffer()); _phrase.Dispose(); _phrase = null; }
            if (_processingAudio != null)
            {
                if (takeProcessingAudio) audio = _processingAudio.ToArray();
                Array.Clear(_processingAudio.GetBuffer()); _processingAudio.Dispose(); _processingAudio = null;
            }
            if (_voiceVisible) VoiceActivityChanged?.Invoke(false);
            _voiceVisible = false;
            _speaking = false; _processing = false; _silenceBytes = 0;
        }
        recognitionSession?.Cancel(); recognitionSession?.Dispose();
        if (input != null) { try { input.StopRecording(); } catch { } input.Dispose(); }
        return audio;
    }

    public void Dispose() => Stop();
}
