namespace VoceLocale.Core;

/// <summary>Allinea sorgenti PCM16 mono 16 kHz allo stesso orologio senza mescolarne il contenuto.</summary>
public sealed class MeetingSourceTimeline : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<MeetingAudioSource, MeetingAudioSegmenter> _sources = new();
    private readonly byte[] _silence = new byte[3200];
    private long _sequence;
    private bool _disposed;
    public event Action<MeetingAudioChunk>? ChunkReady;

    public MeetingSourceTimeline(bool includeSystemAudio)
    {
        Add(MeetingAudioSource.Microphone);
        if (includeSystemAudio) Add(MeetingAudioSource.System);
    }
    private void Add(MeetingAudioSource source)
    {
        var segmenter = new MeetingAudioSegmenter();
        segmenter.ChunkReady += chunk =>
        {
            var routed = chunk with { Sequence = checked(++_sequence), Source = source };
            if (ChunkReady is { } ready) ready(routed); else Array.Clear(routed.Audio);
        };
        _sources.Add(source, segmenter);
    }
    public void Push(MeetingAudioSource source, ReadOnlySpan<byte> pcm, TimeSpan capturedAt)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_sources.TryGetValue(source, out var segmenter)) throw new ArgumentOutOfRangeException(nameof(source));
            if (pcm.Length % 2 != 0 || pcm.Length > 32000 * 5) throw new ArgumentException("Frame PCM non valido o troppo lungo.", nameof(pcm));
            long endSample = ToSamples(capturedAt);
            long expectedStart = Math.Max(0, endSample - pcm.Length / 2);
            // Un margine di 50 ms evita di trasformare il jitter dei callback in buchi audio.
            if (expectedStart - segmenter.CapturedSamples > 800) Advance(segmenter, expectedStart);
            segmenter.Push(pcm);
        }
    }
    public void AdvanceTo(TimeSpan elapsed)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            long target = ToSamples(elapsed);
            foreach (var segmenter in _sources.Values) Advance(segmenter, target);
        }
    }
    private void Advance(MeetingAudioSegmenter segmenter, long target)
    {
        long remaining = target - segmenter.CapturedSamples;
        while (remaining > 0 && segmenter.BufferedBytes > 0)
        {
            int samples = (int)Math.Min(_silence.Length / 2, remaining);
            segmenter.Push(_silence.AsSpan(0, samples * 2));
            remaining = target - segmenter.CapturedSamples;
        }
        if (remaining > 0) segmenter.SkipSilence(TimeSpan.FromSeconds(remaining / 16000d));
    }
    private static long ToSamples(TimeSpan value)
    {
        if (value < TimeSpan.Zero || value.TotalDays > 36525) throw new ArgumentOutOfRangeException(nameof(value));
        return checked((long)Math.Round(value.TotalSeconds * 16000));
    }
    public void Flush()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            foreach (var segmenter in _sources.Values) segmenter.Flush();
        }
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var segmenter in _sources.Values) segmenter.Dispose();
        }
    }
}
