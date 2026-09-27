using System.Text.Json;
using VoceLocale.Core;

internal static class TranslationReplay
{
    public static int Run(string path)
    {
        int checkedCount = 0, failures = 0;
        foreach (string line in File.ReadLines(path))
        {
            if (!line.StartsWith('{')) continue;
            using var item = JsonDocument.Parse(line);
            if (!item.RootElement.TryGetProperty("source", out var source) || !item.RootElement.TryGetProperty("translated", out var target)) continue;
            checkedCount++;
            if (!TranslationPolicy.TryValidate(source.GetString()!, target.GetString(), ["Marco", "Luca", "Giulia"], out _)) failures++;
        }
        Console.WriteLine($"RECORDED_SYNTHETIC_TRANSLATIONS: {checkedCount} checked, {failures} failures");
        return checkedCount == 16 && failures == 0 ? 0 : 1;
    }
}
