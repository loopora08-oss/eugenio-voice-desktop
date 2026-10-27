using System.Text.RegularExpressions;

namespace VoceLocale.Core;

/// <summary>Metadati dichiarati dall'app; non sono una firma o attestazione del contenuto.</summary>
public sealed record RecognitionProvenance(string ModelFile, string ModelSha256, string EngineVersion,
    string DecoderMode, string RequestedLanguage, DateTimeOffset GeneratedAtUtc)
{
    public const string WhisperEngineVersion = "Whisper.net/1.9.1";
    public bool IsValid() => !string.IsNullOrWhiteSpace(ModelFile) && ModelFile.Length <= 128
        && ModelFile.IndexOfAny(['/', '\\', '\0', '\r', '\n']) < 0
        && ModelSha256 != null && ModelSha256.Length == 64 && Regex.IsMatch(ModelSha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant)
        && !string.IsNullOrWhiteSpace(EngineVersion) && EngineVersion.Length <= 128
        && !string.IsNullOrWhiteSpace(DecoderMode) && DecoderMode.Length <= 64
        && RequestedLanguage is "auto" or "it" or "en"
        && GeneratedAtUtc != default && GeneratedAtUtc.Offset == TimeSpan.Zero;
}

public sealed record MeetingAiMetadata(int SchemaVersion, string Tool, string Processing,
    bool HumanReviewRequired, string AsrProvenance, string OtherContentProvenance, string Trust);
