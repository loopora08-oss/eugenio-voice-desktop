namespace VoceLocale.Core;

/// <summary>Diagnostica in memoria: non contiene testo, audio, nomi di finestre o dati personali.</summary>
public sealed record SpeechBlockMetrics(Guid BlockId, int Segments, int RecognitionPasses,
    int AudioBytes, double ConfirmationMilliseconds, double PreparedMilliseconds, double TotalMilliseconds);

public static class TranscriptionResults
{
    /// <summary>Unisce soltanto trascrizioni valide, lasciando i ripensamenti alla revisione finale.</summary>
    public static EngineResponse Combine(IReadOnlyList<EngineResponse> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (results.Count == 0) throw new ArgumentException("Nessuna trascrizione da unire.", nameof(results));
        long elapsed = 0;
        foreach (var result in results)
        {
            if (result == null || !result.Ok || string.IsNullOrWhiteSpace(result.Text))
                throw new InvalidDataException("Un segmento non è stato trascritto. Nessun testo parziale viene confermato.");
            if (result.Milliseconds < 0) throw new InvalidDataException("Tempi di riconoscimento non validi.");
            elapsed = checked(elapsed + result.Milliseconds);
        }
        var language = results.Select(r => r.Language).Distinct(StringComparer.Ordinal).ToArray();
        var backend = results.Select(r => r.Backend).Distinct(StringComparer.Ordinal).ToArray();
        return new EngineResponse(Guid.NewGuid().ToString(), true,
            string.Join(' ', results.Select(r => r.Text!.Trim())), Milliseconds: elapsed,
            Language: language.Length == 1 ? language[0] : null,
            Backend: backend.Length == 1 ? backend[0] : "mixed-local");
    }
}
