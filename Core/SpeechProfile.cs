namespace VoceLocale.Core;

public static class SpeechProfile
{
    // Short phrases scored better with base; longer speech benefits from small's vocabulary.
    public static ModelSpec Choose(string profile, int audioBytes, bool baseAvailable, bool smallAvailable, string? language = null)
    {
        if (!smallAvailable) return Models.FastWhisper;
        if (!baseAvailable || profile == "accurate") return Models.Whisper;
        return profile == "speed" || language == "en" || audioBytes <= 6 * 32000 ? Models.FastWhisper : Models.Whisper;
    }
    public static string DecoderMode(string profile) => profile == "accurate" ? "accurate" : "fast";
}
