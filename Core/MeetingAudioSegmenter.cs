namespace VoceLocale.Core;

public sealed record MeetingAudioChunk(long Sequence, long StartSample, long EndSample, byte[] Audio, bool Overlap)
{
    public double StartSeconds => StartSample / 16000d;
    public double EndSeconds => EndSample / 16000d;
}

/// <summary>Continuous 16 kHz PCM capture with bounded phrase storage, no session-duration limit.</summary>
public sealed class MeetingAudioSegmenter : IDisposable
{
    public const int MaxChunkBytes = 16000 * 2 * 10;
    private const int PreferredChunkBytes = 16000 * 2 * 6;
    private const int MinimumPhraseBytes = 16000 * 2 * 4;
    private const int SilenceBytes = 16000 * 2 * 45 / 100;
    private const int BoundarySilenceBytes = 16000 * 2 * 15 / 100;
    private const int OverlapBytes = 16000 * 2;
    private readonly MemoryStream _phrase = new(MaxChunkBytes + 3200);
    private readonly AudioLeadIn _prefix = new(OverlapBytes);
    private long _samples, _start, _sequence;
    private int _silence;
    private bool _speaking, _nextOverlap, _overlap, _disposed;
    public long CapturedSamples => _samples;
    public int BufferedBytes => checked((int)_phrase.Length);
    public event Action<MeetingAudioChunk>? ChunkReady;

    public void Push(ReadOnlySpan<byte> pcm)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Hardware frames are split as well: a large test/source frame cannot grow the buffer.
        while (!pcm.IsEmpty)
        {
            int count = Math.Min(1600, pcm.Length) & ~1;
            if (count == 0) throw new ArgumentException("PCM16 requires complete samples.", nameof(pcm));
            var frame = pcm[..count]; pcm = pcm[count..];
            long before = _samples; _samples += count / 2;
            bool active = AudioSignal.IsActive(frame);
            if (active)
            {
                if (!_speaking)
                {
                    _prefix.DrainTo(_phrase);
                    _start = Math.Max(0, before - _phrase.Length / 2);
                    _overlap = _nextOverlap; _nextOverlap = false;
                }
                _speaking = true; _silence = 0;
            }
            else if (_speaking) _silence += count;
            else { _prefix.Push(frame); _nextOverlap = false; }
            if (_speaking) _phrase.Write(frame);
            // Prefer an actual pause over cutting every six seconds in the middle of a word.
            if (_speaking && _phrase.Length >= MinimumPhraseBytes &&
                (_silence >= SilenceBytes || _phrase.Length >= PreferredChunkBytes && _silence >= BoundarySilenceBytes))
                Emit(cut: false, trim: Math.Min(_silence / 2, 16000 * 2 * 2 / 10));
            else if (_phrase.Length >= MaxChunkBytes) Emit(cut: true, trim: 0);
        }
    }

    private void Emit(bool cut, int trim)
    {
        int length = Math.Max(0, (int)_phrase.Length - trim) & ~1;
        byte[]? audio = null;
        if (length > 0 && TextPolicy.HasVoice(_phrase.GetBuffer().AsSpan(0, length)))
            audio = _phrase.GetBuffer().AsSpan(0, length).ToArray();
        if (cut && audio != null) { _prefix.Push(audio.AsSpan(Math.Max(0, audio.Length - OverlapBytes))); _nextOverlap = true; }
        Array.Clear(_phrase.GetBuffer()); _phrase.SetLength(0);
        _speaking = false; _silence = 0;
        if (audio != null)
        {
            var chunk = new MeetingAudioChunk(++_sequence, _start, _samples - trim / 2, audio, _overlap);
            if (ChunkReady is { } ready) ready(chunk); else Array.Clear(audio);
        }
    }

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_phrase.Length > 0) Emit(cut: false, trim: 0);
        _prefix.Clear();
        _nextOverlap = _overlap = false;
    }

    public void SkipSilence(TimeSpan pause)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_speaking || _phrase.Length > 0) throw new InvalidOperationException("Flush capture before advancing the meeting clock.");
        if (pause < TimeSpan.Zero || !double.IsFinite(pause.TotalSeconds)) throw new ArgumentOutOfRangeException(nameof(pause));
        _samples = checked(_samples + (long)Math.Round(pause.TotalSeconds * 16000));
        _prefix.Clear(); _nextOverlap = _overlap = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _prefix.Clear(); Array.Clear(_phrase.GetBuffer()); _phrase.Dispose();
    }
}
