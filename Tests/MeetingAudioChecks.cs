using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using VoceLocale;
using VoceLocale.Core;

internal static class MeetingAudioChecks
{
    public static async Task<int> RunAsync(string engineRoot, string fixtures, bool small)
    {
        using var engine = new EngineClient("WhisperWorker", Path.GetFullPath(engineRoot), realtime: true);
        using var text = new OllamaTextService(); using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        int failures = 0;
        foreach (string file in new[] { "translation-0.wav", "translation-1.wav", "translation-2.wav", "it-correction.wav", "en-correction.wav" })
        {
            byte[] audio = ReadPcm(Path.Combine(fixtures, file));
            try
            {
                var result = await engine.RunAsync(new(Guid.NewGuid().ToString(), "transcribe", Storage.ModelPath(small ? Models.Whisper : Models.FastWhisper), "auto", audio, Mode: "fast"), cancel.Token);
                string source = result.Text ?? "";
                bool faithful = result.Ok && file switch
                {
                    "translation-0.wav" => Regex.IsMatch(source, @"\b(?:50|cinquanta)\b", RegexOptions.IgnoreCase) && Regex.IsMatch(source, @"\b(?:15|quindici)\b", RegexOptions.IgnoreCase) && source.Contains("non", StringComparison.OrdinalIgnoreCase) && source.Contains("annull", StringComparison.OrdinalIgnoreCase),
                    "translation-1.wav" => source.Contains("Marco") && source.Contains("quanto", StringComparison.OrdinalIgnoreCase) && source.Contains('?'),
                    "translation-2.wav" => source.Contains("Luca") && source.Contains("disponibile", StringComparison.OrdinalIgnoreCase) && source.Contains("non", StringComparison.OrdinalIgnoreCase) && source.Contains('?'),
                    "it-correction.wav" => source.Contains("martedì", StringComparison.OrdinalIgnoreCase) && source.Contains("giovedì", StringComparison.OrdinalIgnoreCase) && source.Contains("non", StringComparison.OrdinalIgnoreCase),
                    _ => source.Contains("Tuesday", StringComparison.OrdinalIgnoreCase) && source.Contains("Thursday", StringComparison.OrdinalIgnoreCase) && source.Contains("not", StringComparison.OrdinalIgnoreCase)
                };
                var translated = await text.TranslateAsync("translategemma:4b", source, ["Marco", "Luca"], cancel.Token, result.Language);
                string english = translated.Text ?? "";
                faithful &= translated.Ok;
                if (file == "translation-0.wav") faithful &= english.Contains("cancel", StringComparison.OrdinalIgnoreCase) && (english.Contains("sorry", StringComparison.OrdinalIgnoreCase) || english.Contains("excuse", StringComparison.OrdinalIgnoreCase));
                if (file == "translation-1.wav") faithful &= english.Contains("Marco") && english.Contains("email", StringComparison.OrdinalIgnoreCase) && english.Contains('?') && !english.Contains("four", StringComparison.OrdinalIgnoreCase);
                if (file == "translation-2.wav") faithful &= english.Contains("Luca") && english.Contains("you", StringComparison.OrdinalIgnoreCase) && english.Contains("available", StringComparison.OrdinalIgnoreCase) && !english.Contains("she", StringComparison.OrdinalIgnoreCase);
                if (file == "en-correction.wav") faithful &= english == TextPolicy.ToSingleParagraph(source);
                if (!faithful) failures++;
                Console.WriteLine(JsonSerializer.Serialize(new { synthetic = true, file, small, faithful, source, english, result.Language, result.Backend, transcriptionMs = result.Milliseconds, translationMs = translated.Milliseconds }));
            }
            finally { Array.Clear(audio); }
        }
        Console.WriteLine($"MEETING_AUDIO: {failures} failures"); return failures == 0 ? 0 : 1;
    }
    public static byte[] ReadPcm(string file)
    {
        using var reader = new BinaryReader(File.OpenRead(file));
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidDataException();
        reader.ReadInt32(); if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new InvalidDataException();
        bool format = false;
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            string id = Encoding.ASCII.GetString(reader.ReadBytes(4)); int length = reader.ReadInt32();
            if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException();
            if (id == "fmt ") { var f = reader.ReadBytes(length); format = f.Length >= 16 && BitConverter.ToInt16(f, 0) == 1 && BitConverter.ToInt16(f, 2) == 1 && BitConverter.ToInt32(f, 4) == 16000 && BitConverter.ToInt16(f, 14) == 16; }
            else if (id == "data") { if (!format) throw new InvalidDataException("Synthetic fixture must be PCM16 mono 16kHz."); return reader.ReadBytes(length); }
            else reader.BaseStream.Seek(length, SeekOrigin.Current);
            if ((length & 1) != 0) reader.BaseStream.Seek(1, SeekOrigin.Current);
        }
        throw new InvalidDataException();
    }
}
