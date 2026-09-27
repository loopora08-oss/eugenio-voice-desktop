namespace VoceLocale.Core;

/// <summary>Bounded PCM history indexed by capture position, so queued wake checks
/// can hand over exactly the audio following their own window without duplicates.</summary>
public sealed class WakeAudioHistory(int capacity = 16000 * 2 * 120)
{
    private readonly byte[] _buffer = new byte[capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity))];
    public long Position { get; private set; }
    public void Append(ReadOnlySpan<byte> audio)
    {
        long end = Position + audio.Length;
        if (audio.Length > _buffer.Length) { Position += audio.Length - _buffer.Length; audio = audio[^_buffer.Length..]; }
        int start = (int)(Position % _buffer.Length);
        int first = Math.Min(audio.Length, _buffer.Length - start);
        audio[..first].CopyTo(_buffer.AsSpan(start));
        audio[first..].CopyTo(_buffer);
        Position = end;
    }
    public byte[] After(long position)
    {
        if (position < Math.Max(0, Position - _buffer.Length) || position > Position)
            throw new ArgumentOutOfRangeException(nameof(position));
        var result = new byte[checked((int)(Position - position))];
        int start = (int)(position % _buffer.Length);
        int first = Math.Min(result.Length, _buffer.Length - start);
        _buffer.AsSpan(start, first).CopyTo(result);
        _buffer.AsSpan(0, result.Length - first).CopyTo(result.AsSpan(first));
        return result;
    }
    public void Clear() { Array.Clear(_buffer); Position = 0; }
}
