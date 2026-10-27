using System.Text.Json;
using VoceLocale.Core;

internal static class MeetingSearchChecks
{
    public static void Run(Action<string, bool> check)
    {
        var doc = new MeetingDocument
        {
            Entries = [
                new(1, 0, 2, "Il caffè sarà pronto domani.", "Coffee will be ready tomorrow."),
                new(2, 2, 4, "Non confermare il preventivo.", "Do not confirm the quote.", Source: MeetingAudioSource.System),
                new(3, 5, 7, "Conferma soltanto il caffè.", "Confirm only the coffee.")]
        };
        check("ricerca senza distinzione di maiuscole o accenti", MeetingSearch.Find(doc.Entries, "CAFFE").SequenceEqual([0, 2]));
        check("ricerca inglese e conteggio per blocco", MeetingSearch.Find(doc.Entries, "coffee").SequenceEqual([0, 2]) && MeetingSearch.Find(doc.Entries, "confirm").SequenceEqual([1, 2]));
        check("frase completa mantiene negazione e contesto", MeetingSearch.Find(doc.Entries, "Non confermare").SequenceEqual([1]));
        check("ricerca vuota e documento vuoto distinti dal contenuto", MeetingSearch.Find(doc.Entries, " ").Count == 0 && MeetingSearch.Find([], "caffè").Count == 0);
        check("nessuna corrispondenza restituisce zero", MeetingSearch.Find(doc.Entries, "locomotiva").Count == 0);
        check("query con sintassi regex resta letterale", MeetingSearch.Find(doc.Entries, ".*").Count == 0 && MeetingSearch.Find(doc.Entries, "[").Count == 0);
        check("caratteri invisibili non selezionano ogni blocco", MeetingSearch.Find(doc.Entries, "\u200b").Count == 0);
        check("navigazione circolare avanti e indietro", MeetingSearch.Move(1, 2, 1) == 0 && MeetingSearch.Move(0, 2, -1) == 1 && MeetingSearch.Move(-1, 2, 1) == 0 && MeetingSearch.Move(-1, 0, 1) == -1);
        var restored = JsonSerializer.Deserialize<MeetingDocument>(JsonSerializer.Serialize(doc))!;
        check("ricerca dopo riapertura preserva documento e sorgente", restored.IsValid() && MeetingSearch.Find(restored.Entries, "preventivo").SequenceEqual([1]) && restored.Entries[1].Source == MeetingAudioSource.System);
        doc.Entries.Add(new(4, 8, 10, "Altro caffè domani."));
        check("nuovi blocchi inclusi nella ricerca", MeetingSearch.Find(doc.Entries, "caffè").SequenceEqual([0, 2, 3]));
        doc.Entries[1] = doc.Entries[1] with { English = "Please confirm the coffee order." };
        check("traduzione aggiunta aggiorna i risultati senza duplicare blocchi", MeetingSearch.Find(doc.Entries, "coffee").SequenceEqual([0, 1, 2]));
        try { MeetingSearch.Find(doc.Entries, new string('x', 201)); check("query troppo lunga rifiutata", false); }
        catch (ArgumentException) { check("query troppo lunga rifiutata", true); }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { MeetingSearch.Find(doc.Entries, "caffè", cancelled.Token); check("ricerca annullabile", false); }
        catch (OperationCanceledException) { check("ricerca annullabile", true); }
        check("ricerca non modifica o salva il documento", doc.Entries.Count == 4 && doc.Notes == "" && doc.Entries[0].Original == "Il caffè sarà pronto domani.");
    }
}
