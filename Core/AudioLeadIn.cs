namespace VoceLocale.Core;

/// <summary>Retains a short in-memory prefix so energy gating does not cut word beginnings.</summary>
public sealed class AudioLeadIn(int capacity = 9600)
{
    private readonly byte[] _buffer = new byte[capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity))];
    private int _length;
    public void Push(ReadOnlySpan<byte> audio)
    {
        if (audio.Length >= _buffer.Length)
        { audio[^_buffer.Length..].CopyTo(_buffer); _length = _buffer.Length; return; }
        int keep = Math.Min(_length, _buffer.Length - audio.Length);
        _buffer.AsSpan(_length - keep, keep).CopyTo(_buffer);
        audio.CopyTo(_buffer.AsSpan(keep)); _length = keep + audio.Length;
    }
    public void DrainTo(Stream destination) { destination.Write(_buffer, 0, _length); Clear(); }
    public void Clear() { Array.Clear(_buffer); _length = 0; }
}
