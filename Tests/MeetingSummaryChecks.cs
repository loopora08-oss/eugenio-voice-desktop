using System.Text.Json;
using VoceLocale.Core;

internal static class MeetingSummaryChecks
{
    public static async Task SafetyAsync(Action<string,bool> check)
    {
        var document=new MeetingDocument{Notes="Appunti personali intatti",Summary="Riepilogo precedente"};
        try
        {
            document.Summary=await MeetingSummaryComposer.GenerateAsync(["primo","secondo"],
                (part,token)=>Task.FromResult((true,(string?)(part=="primo"?"Valido":null),false)),CancellationToken.None);
            check("errore nel secondo brano non pubblica un riepilogo parziale",false);
        }
        catch(InvalidDataException) {check("errore nel secondo brano non pubblica un riepilogo parziale",document.Summary=="Riepilogo precedente"&&document.Notes=="Appunti personali intatti");}
        try
        {
            await MeetingSummaryComposer.GenerateAsync(["testo"],(part,token)=>Task.FromResult((true,(string?)"Estratto",true)),CancellationToken.None);
            check("estratti non spacciati per riepilogo intelligente",false);
        }
        catch(InvalidDataException) {check("estratti non spacciati per riepilogo intelligente",true);}
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        try {await MeetingSummaryComposer.GenerateAsync(["testo"],(part,token)=>Task.FromResult((true,(string?)"Output",false)),cancelled.Token);check("annullamento non sostituisce gli appunti",false);}
        catch(OperationCanceledException) {check("annullamento non sostituisce gli appunti",document.Summary=="Riepilogo precedente");}
        using var missing=new VoceLocale.OllamaTextService(new ReplyHandler(System.Net.HttpStatusCode.ServiceUnavailable,"{}"));
        var unavailable=await missing.NotesAsync("fixture","Marco invierà il documento giovedì.",CancellationToken.None);
        check("provider non disponibile produce solo fallback riconoscibile",unavailable.ExtractiveFallback&&unavailable.Candidate==null);
        using var empty=new VoceLocale.OllamaTextService(new ReplyHandler(System.Net.HttpStatusCode.OK,"{\"done\":true,\"message\":{\"content\":\"\"}}"));
        var noText=await empty.NotesAsync("fixture","Marco invierà il documento giovedì.",CancellationToken.None);
        check("risposta vuota non diventa riepilogo valido",noText.ExtractiveFallback);
        var noModel=await empty.NotesAsync("","Appunti",CancellationToken.None);
        var huge=await empty.NotesAsync("fixture",new string('x',8001),CancellationToken.None);
        check("modello mancante e input fuori budget rifiutati",!noModel.Ok&&!huge.Ok);
    }
    private sealed class ReplyHandler(System.Net.HttpStatusCode status,string body):System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken token)
        {
            if(request.RequestUri?.Host!="127.0.0.1") throw new InvalidOperationException();
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(status){Content=new System.Net.Http.StringContent(body,System.Text.Encoding.UTF8,"application/json")});
        }
    }
    public static void Run(Action<string,bool> check)
    {
        var entries=new List<MeetingEntry>{new(1,0,3,"Non annullare la prova.")};
        var input=MeetingSummaryInput.Build(entries,"Chiedere a Elena il costo.");
        check("riepilogo include trascrizione e appunti manuali",input.Count==1&&input[0].Contains("Non annullare")&&input[0].Contains("Chiedere a Elena"));
        check("riepilogo possibile senza audio da soli appunti",MeetingSummaryInput.Build([],"Usare il piano gratuito.").Single().Contains("piano gratuito"));
        check("nessun input non genera un riepilogo inventato",MeetingSummaryInput.Build([]," ").Count==0);
        string longNote=string.Concat(Enumerable.Repeat("parola ",2000))+"ULTIMA_PAROLA";
        var groups=MeetingSummaryInput.Build([],longNote,600);
        string joined=string.Join("",groups);
        check("appunti lunghi suddivisi senza perdere la coda",groups.All(x=>x.Length<=600)&&joined=="appunti personali:\n"+longNote);
        var doc=new MeetingDocument{Notes="Appunti personali",Summary="Riepilogo separato",SummaryNeedsUpdate=true,SummaryEdited=true};
        var restored=JsonSerializer.Deserialize<MeetingDocument>(doc.Json())!;
        check("appunti e riepilogo salvati separatamente con stato da aggiornare",restored.Notes==doc.Notes&&restored.Summary==doc.Summary&&restored.SummaryNeedsUpdate&&restored.SummaryEdited);
        check("export Markdown contiene entrambe le sezioni",doc.Markdown().Contains("## Riepilogo IA")&&doc.Markdown().Contains(doc.Notes)&&doc.Markdown().Contains(doc.Summary));
        check("vecchi documenti restano apribili senza riepilogo inventato",JsonSerializer.Deserialize<MeetingDocument>("{\"Notes\":\"Vecchi appunti\"}") is {Summary:""} legacy&&legacy.IsValid());
        check("riepilogo nullo invalido",!new MeetingDocument{Summary=null!}.IsValid());
        check("negazione espressa con nessuna resta valida",MeetingNotesPolicy.TryValidate("Non acquistiamo nuove licenze.","Nessuna nuova licenza acquistata.",out _));
        check("modalità incerta non richiede la stessa parola letterale",MeetingNotesPolicy.TryValidate("Forse Sara potrà verificare il contratto.","Sara potrebbe verificare il contratto.",out _));
        check("incertezza rimossa rifiutata",!MeetingNotesPolicy.TryValidate("Forse Sara potrà verificare il contratto.","Sara verificherà il contratto.",out _));
        check("attività futura inglese resta attività",MeetingNotesPolicy.TryValidate("Anna will check the integration on Thursday.","Action item: Anna to check the integration on Thursday.",out _));
        check("titolo attività non permette di dichiararla completata",!MeetingNotesPolicy.TryValidate("Anna will check the integration on Thursday.","Action item: Anna has completed the integration on Thursday.",out _));
        check("chi riceve una domanda non diventa chi la pone",!MeetingNotesPolicy.TryValidate("Chiedere a Elena il costo.","Chiedere il costo: richiesta da parte di Elena.",out _));
        check("domanda ancora da fare non dichiarata già posta",!MeetingNotesPolicy.TryValidate("Chiedere a Elena il costo.","Chiesta a Elena una conferma sul costo. Chiedere il dettaglio.",out _));
    }
}
