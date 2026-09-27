namespace VoceLocale.Core;

public static class AudioSignal
{
    // Energy per frame, rather than requiring most waveform samples to exceed a
    // fixed amplitude. Speech has zero crossings even when it is clearly audible.
    public const double MinimumSpeechRms = 60;
    public static double Rms(ReadOnlySpan<byte> pcm)
    {
        double energy = 0;
        int count = pcm.Length / 2;
        for (int i = 0; i < count; i++)
        {
            short value = (short)(pcm[2 * i] | pcm[2 * i + 1] << 8);
            energy += (double)value * value;
        }
        return count == 0 ? 0 : Math.Sqrt(energy / count);
    }
    public static bool IsActive(ReadOnlySpan<byte> pcm) => Rms(pcm) >= MinimumSpeechRms;
}
