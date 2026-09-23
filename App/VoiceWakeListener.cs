using System.IO;
using NAudio.Wave;

namespace VoceLocale;

/// <summary>On-device voice-activity gate. Audio is held only until the wake check completes.</summary>
public sealed class VoiceWakeListener : IDisposable
{
    private readonly object _gate = new();
    private WaveInEvent? _input;
    private MemoryStream? _phrase;
    private Func<byte[], Task>? _recognize;
    private int _silenceBytes;
    private bool _speaking, _processing, _disposed, _voiceVisible;
    private const int SilenceLimit = 16000 * 2 * 7 / 10;
    private const int PhraseLimit = 16000 * 2 * 8;
    public event Action? Failed;
    public event Action<bool>? VoiceActivityChanged;

    public void Start(int device, Func<byte[], Task> recognize)
    {
        Stop();
        lock (_gate)
        {
            _disposed = false; _recognize = recognize;
            _phrase = new MemoryStream(PhraseLimit); _silenceBytes = 0; _speaking = _processing = _voiceVisible = false;
            _input = new WaveInEvent { DeviceNumber = device, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 50 };
            _input.DataAvailable += OnData;
            _input.RecordingStopped += (_, e) => { if (e.Exception != null) Failed?.Invoke(); };
            try { _input.StartRecording(); }
            catch { _input.Dispose(); _input = null; throw; }
        }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        byte[]? segment = null;
        bool? voiceChanged = null;
        lock (_gate)
        {
            if (_disposed || _phrase == null || _processing) { Array.Clear(e.Buffer); return; }
            var bytes = e.BytesRecorded & ~1;
            if (bytes == 0) { Array.Clear(e.Buffer); return; }
            int voiced = 0;
            for (int i = 0; i < bytes; i += 2)
            {
                short sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                if (Math.Abs((int)sample) > 320) voiced++;
            }
            bool active = voiced > bytes / 4;
            if (active) { _speaking = true; _silenceBytes = 0; }
            else if (_speaking) _silenceBytes += bytes;
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
                Array.Clear(_phrase.GetBuffer()); _phrase.SetLength(0);
                _silenceBytes = 0; _speaking = false; _processing = true;
            }
        }
        if (voiceChanged is { } detected) VoiceActivityChanged?.Invoke(detected);
        if (segment != null) _ = ProcessAsync(segment);
    }

    private async Task ProcessAsync(byte[] segment)
    {
        try
        {
            Func<byte[], Task>? recognize;
            lock (_gate) recognize = _recognize;
            if (recognize != null) await recognize(segment).ConfigureAwait(false);
        }
        catch { Failed?.Invoke(); }
        finally
        {
            Array.Clear(segment);
            lock (_gate) _processing = false;
        }
    }

    public void Stop()
    {
        WaveInEvent? input;
        lock (_gate)
        {
            _disposed = true; _recognize = null;
            input = _input; _input = null;
            if (_phrase != null) { Array.Clear(_phrase.GetBuffer()); _phrase.Dispose(); _phrase = null; }
            if (_voiceVisible) VoiceActivityChanged?.Invoke(false);
            _voiceVisible = false;
            _speaking = false; _processing = false; _silenceBytes = 0;
        }
        if (input != null) { try { input.StopRecording(); } catch { } input.Dispose(); }
    }

    public void Dispose() => Stop();
}
