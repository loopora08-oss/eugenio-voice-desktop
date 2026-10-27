using System.Text.Json;
using System.Threading.Channels;
using VoceLocale.Core;
using VoceLocale;

internal static class IncrementalSpeechChecks
{
    public static async Task RunAsync(Action<string, bool> check)
    {
        int personalReads = 0;
        var isolated = Storage.LoadSettings(previewOnly: true, readSettings: () => { personalReads++; throw new IOException(); });
        check("l'anteprima non legge impostazioni o dizionari personali", personalReads == 0 && isolated.Vocabulary.Length == 0);
        var configured = Storage.LoadSettings(readSettings: () => "{\"Language\":\"it\",\"Vocabulary\":[\"Eugenio\"]}");
        check("l'avvio normale conserva le impostazioni esplicite", configured.Language == "it" && configured.Vocabulary.SequenceEqual(new[] { "Eugenio" }));
        check("impostazioni danneggiate non impediscono l'avvio", Storage.LoadSettings(readSettings: () => "non json").Vocabulary.Length == 0);
        var queue = Channel.CreateUnbounded<byte[]>();
        byte[] first = [1], second = [2], third = [3];
        foreach (var audio in new[] { first, second, third }) queue.Writer.TryWrite(audio);
        queue.Writer.TryComplete();
        var inputs = new List<string>();
        var published = new List<string>();
        byte[]? publishedAudio = null;
        SpeechBlockMetrics? metrics = null;
        await SpeechBlockQueue.RunPreparedAsync(queue.Reader, TimeSpan.FromSeconds(10),
            (audio, _) =>
            {
                inputs.Add(string.Join(',', audio));
                string text = audio[0] switch { 1 => "Ci vediamo martedì.", 2 => "No, giovedì.", _ => "Non annullare." };
                return Task.FromResult(new EngineResponse(Guid.NewGuid().ToString(), true, text, Milliseconds: 15, Language: "it"));
            },
            (audio, result, _) => { publishedAudio = audio.ToArray(); published.Add(result.Text!); return Task.CompletedTask; }, CancellationToken.None,
            combinePrepared: TranscriptionResults.Combine, measured: m => metrics = m);
        check("ogni segmento incrementale viene riconosciuto una sola volta", inputs.SequenceEqual(new[] { "1", "2", "3" }));
        check("ripensamenti e negazioni tra segmenti arrivano a un'unica revisione", published.SequenceEqual(new[] { "Ci vediamo martedì. No, giovedì. Non annullare." }));
        check("il risultato aggregato resta correlato all'intero audio del blocco", publishedAudio != null && publishedAudio.SequenceEqual(new byte[] { 1, 2, 3 }));
        if (publishedAudio != null) Array.Clear(publishedAudio);
        check("i buffer incrementali sono azzerati dopo la conferma", first[0] == 0 && second[0] == 0 && third[0] == 0);
        check("la chiusura esplicita non attende la finestra da dieci secondi", metrics is { TotalMilliseconds: < 5000, Segments: 3, RecognitionPasses: 3 });
        check("le metriche esportabili non contengono trascrizioni o audio", metrics != null && !JsonSerializer.Serialize(metrics).Contains("giovedì") && metrics.AudioBytes == 3);

        var preparationRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var eager = Channel.CreateUnbounded<byte[]>(); int starts = 0, eagerPublished = 0;
        eager.Writer.TryWrite([4]); eager.Writer.TryWrite([5]);
        var eagerRun = SpeechBlockQueue.RunPreparedAsync(eager.Reader, TimeSpan.FromSeconds(10), async (audio, token) =>
        {
            if (Interlocked.Increment(ref starts) == 2) bothStarted.TrySetResult();
            await preparationRelease.Task.WaitAsync(token);
            return (int)audio[0];
        }, (_, _, _) => { eagerPublished++; return Task.CompletedTask; }, CancellationToken.None,
            combinePrepared: results => results.Sum());
        await bothStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        check("i nuovi segmenti entrano nel riconoscimento prima della conferma", starts == 2 && eagerPublished == 0);
        eager.Writer.TryComplete(); preparationRelease.TrySetResult(); await eagerRun;
        check("la preparazione anticipata pubblica una sola volta", eagerPublished == 1);

        var failure = Channel.CreateUnbounded<byte[]>(); byte[] good = [6], bad = [7], pending = [8];
        failure.Writer.TryWrite(good); failure.Writer.TryWrite(bad); failure.Writer.TryWrite(pending); failure.Writer.TryComplete();
        int failurePublished = 0; bool rejected = false;
        try
        {
            await SpeechBlockQueue.RunPreparedAsync(failure.Reader, TimeSpan.FromMilliseconds(50),
                (audio, _) => Task.FromResult(new EngineResponse("test", audio[0] != 7, audio[0] == 7 ? null : "Testo")),
                (_, _, _) => { failurePublished++; return Task.CompletedTask; }, CancellationToken.None,
                combinePrepared: TranscriptionResults.Combine);
        }
        catch (InvalidDataException) { rejected = true; }
        check("un segmento non riconosciuto impedisce la conferma parziale", rejected && failurePublished == 0);
        check("un errore azzera anche i segmenti successivi", good[0] == 0 && bad[0] == 0 && pending[0] == 0);

        var over = Channel.CreateUnbounded<byte[]>(); byte[] a = [9, 9], b = [8, 8], tail = [7, 7];
        foreach (var audio in new[] { a, b, tail }) over.Writer.TryWrite(audio);
        over.Writer.TryComplete(); bool limited = false; int afterLimit = 0;
        try
        {
            await SpeechBlockQueue.RunPreparedAsync(over.Reader, TimeSpan.FromMilliseconds(50),
                (audio, _) => Task.FromResult(audio.Length), (_, _, _) => { afterLimit++; return Task.CompletedTask; },
                CancellationToken.None, combinePrepared: values => values.Sum(), maximumAudioBytes: 3);
        }
        catch (InvalidDataException) { limited = true; }
        check("il budget audio impedisce blocchi di dimensione illimitata", limited && afterLimit == 0);
        check("superare il budget azzera il blocco e svuota la coda", a.All(x => x == 0) && b.All(x => x == 0) && tail.All(x => x == 0));

        var cancellation = Channel.CreateUnbounded<byte[]>(); byte[] cancelled = [10];
        using var cancel = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelledRun = SpeechBlockQueue.RunPreparedAsync(cancellation.Reader, TimeSpan.FromSeconds(10), async (audio, token) =>
        { started.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); return 1; },
            (_, _, _) => throw new Exception("Non deve pubblicare dopo annullamento."), cancel.Token,
            combinePrepared: values => values.Sum());
        cancellation.Writer.TryWrite(cancelled); await started.Task.WaitAsync(TimeSpan.FromSeconds(3)); cancel.Cancel();
        try { await cancelledRun; } catch (OperationCanceledException) { }
        check("annullare una preparazione incrementale azzera l'audio", cancelled[0] == 0);

        var observer = Channel.CreateUnbounded<byte[]>(); observer.Writer.TryWrite([11]); observer.Writer.TryComplete(); int confirmations = 0;
        await SpeechBlockQueue.RunPreparedAsync(observer.Reader, TimeSpan.FromSeconds(1), (audio, _) => Task.FromResult(1),
            (_, _, _) => { confirmations++; return Task.CompletedTask; }, CancellationToken.None,
            combinePrepared: values => values.Sum(), measured: _ => throw new IOException("Diagnostica indisponibile."));
        check("un osservatore guasto non ripete un inserimento confermato", confirmations == 1);

        var merged = TranscriptionResults.Combine([new("a", true, "Hello", Milliseconds: 2, Language: "en"), new("b", true, "ciao", Milliseconds: 3, Language: "it")]);
        check("lingue miste conservate senza attribuzione linguistica inventata", merged.Text == "Hello ciao" && merged.Language == null && merged.Milliseconds == 5);
        bool emptyRejected = false, invalidTimeRejected = false;
        try { TranscriptionResults.Combine([]); } catch (ArgumentException) { emptyRejected = true; }
        try { TranscriptionResults.Combine([new("a", true, "test", Milliseconds: -1)]); } catch (InvalidDataException) { invalidTimeRejected = true; }
        check("aggregazione rifiuta input vuoto e tempi non validi", emptyRejected && invalidTimeRejected);
    }
}
