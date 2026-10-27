using System.Text.Json;
using VoceLocale.Core;

internal static class ProvenanceChecks
{
    public static async Task RunAsync(Action<string, bool> check)
    {
        var trace = new RecognitionProvenance(Models.Whisper.FileName, Models.Whisper.Sha256, "Whisper.net/1.9.1",
            "fast", "it", new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero));
        var doc = new MeetingDocument { Entries = [new(1, 0, 1, "Non annullare.", Provenance: trace)] };
        check("provenienza ASR con hash e versione puntuali valida", trace.IsValid() && doc.IsValid() && doc.AiMetadata.AsrProvenance == "recorded");
        check("hash assente o con suffisso rifiutato", !(trace with { ModelSha256 = "" }).IsValid() && !(trace with { ModelSha256 = trace.ModelSha256 + "\n" }).IsValid());
        check("percorso locale del modello non esportabile", !(trace with { ModelFile = @"C:\personale\modello.bin" }).IsValid());
        check("timestamp non UTC o assente rifiutato", !(trace with { GeneratedAtUtc = default }).IsValid() && !(trace with { GeneratedAtUtc = trace.GeneratedAtUtc.ToOffset(TimeSpan.FromHours(1)) }).IsValid());
        check("versione o lingua non dichiarate rifiutate", !(trace with { EngineVersion = "" }).IsValid() && !(trace with { RequestedLanguage = "" }).IsValid());
        string json = doc.Json();
        using var parsed = JsonDocument.Parse(json);
        check("marcatura AI presente nell'artefatto JSON finale", parsed.RootElement.GetProperty("AiMetadata").GetProperty("HumanReviewRequired").GetBoolean()
            && parsed.RootElement.GetProperty("Entries")[0].GetProperty("Provenance").GetProperty("ModelSha256").GetString() == Models.Whisper.Sha256);
        var restored = JsonSerializer.Deserialize<MeetingDocument>(json)!;
        check("riapertura conserva provenienza e testo", restored.IsValid() && restored.Entries[0].Provenance == trace && restored.Entries[0].Original == "Non annullare.");
        string markdown = doc.Markdown();
        string marker = "<!-- eugenio-ai-metadata: ";
        int metadataEnd = markdown.IndexOf(" -->", StringComparison.Ordinal);
        var metadata = JsonSerializer.Deserialize<MeetingAiMetadata>(markdown[marker.Length..metadataEnd]);
        string entryMarker = "<!-- eugenio-asr-provenance: ";
        int entryStart = markdown.IndexOf(entryMarker, StringComparison.Ordinal) + entryMarker.Length;
        int entryEnd = markdown.IndexOf(" -->", entryStart, StringComparison.Ordinal);
        var exportedTrace = JsonSerializer.Deserialize<RecognitionProvenance>(markdown[entryStart..entryEnd]);
        check("marcatura e provenienza leggibili dal Markdown finale", metadata?.Processing == "ai-assisted-workflow" && exportedTrace == trace && markdown.Contains("Verifica trascrizione"));
        var legacy = JsonSerializer.Deserialize<MeetingDocument>("{\"Entries\":[{\"Sequence\":1,\"StartSeconds\":0,\"EndSeconds\":1,\"Original\":\"Ciao\"}],\"AiMetadata\":{\"AsrProvenance\":\"recorded\"}}")!;
        check("vecchi file aperti senza inventare o fidarsi di provenienza in ingresso", legacy.IsValid() && legacy.Entries[0].Provenance == null && legacy.AiMetadata.AsrProvenance == "unavailable");
        doc.Entries.Add(new(2, 2, 3, "Vecchio blocco."));
        check("documenti misti dichiarano provenienza ASR parziale", doc.AiMetadata.AsrProvenance == "partial" && doc.AiMetadata.OtherContentProvenance == "not-recorded");
        check("metadati non sono presentati come firma", doc.AiMetadata.Trust == "self-reported-not-signed");
        using var stream = new MeetingStream((chunk, token) => Task.FromResult(new EngineResponse("fixture", true,
            "Non annullare.", Provenance: trace)), (text, token) => Task.FromResult<string?>("Do not cancel."));
        stream.Submit(new(1, 0, 16000, new byte[32000], false), true);
        await stream.CompleteAsync();
        check("pipeline e traduzione conservano provenienza del blocco originale", stream.Snapshot().Single() is { English: "Do not cancel." } entry && entry.Provenance == trace);
        var invalid = new MeetingDocument { Entries = [new(1, 0, 1, "Testo", Provenance: trace with { ModelSha256 = "invalid" })] };
        check("file con provenienza malformata rifiutato", !invalid.IsValid());
        try { invalid.Json(); check("export JSON non maschera provenienza invalida", false); }
        catch (InvalidDataException) { check("export JSON non maschera provenienza invalida", true); }
        try { invalid.Markdown(); check("export Markdown non maschera provenienza invalida", false); }
        catch (InvalidDataException) { check("export Markdown non maschera provenienza invalida", true); }
    }
}
