using System.Net;
using System.Net.Http;
using System.Text;
using VoceLocale;

internal static class RewriteFallbackChecks
{
    public static async Task RunAsync(Action<string, bool> check)
    {
        using var empty = new OllamaRewriter(new ReplyHandler("{\"message\":{\"role\":\"assistant\",\"content\":\"\"},\"done\":true,\"done_reason\":\"stop\"}"));
        var fallback = await empty.RewriteAsync("fixture", "Non annullare la prenotazione.", "general", null, CancellationToken.None);
        check("una risposta vuota conserva l'originale con fallback esplicito", fallback.Ok && fallback.UsedOriginalFallback && fallback.Text == "Non annullare la prenotazione.");
        var unclear = await empty.RewriteAsync("fixture", "Era bello, no, diverso.", "general", null, CancellationToken.None);
        check("una correzione ambigua non viene dichiarata riuscita dal fallback", !unclear.Ok && !unclear.UsedOriginalFallback);
        using var truncated = new OllamaRewriter(new ReplyHandler("{\"message\":{\"role\":\"assistant\",\"content\":\"\"},\"done\":true,\"done_reason\":\"length\"}"));
        var incomplete = await truncated.RewriteAsync("fixture", "Testo da correggere.", "general", null, CancellationToken.None);
        check("un output troncato non viene mascherato da testo originale", !incomplete.Ok && !incomplete.UsedOriginalFallback);
        using var malformed = new OllamaRewriter(new ReplyHandler("{\"done\":true}"));
        var missing = await malformed.RewriteAsync("fixture", "Testo da correggere.", "general", null, CancellationToken.None);
        check("una risposta senza messaggio produce un errore controllato", !missing.Ok && !missing.UsedOriginalFallback);
        using var unavailable = new OllamaRewriter(new ReplyHandler("{}", HttpStatusCode.ServiceUnavailable));
        var failed = await unavailable.RewriteAsync("fixture", "Testo da correggere.", "general", null, CancellationToken.None);
        check("servizio indisponibile non inventa una correzione", !failed.Ok && failed.Text == null);
    }
    private sealed class ReplyHandler(string content, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri?.Host != "127.0.0.1") throw new InvalidOperationException("Le richieste devono restare locali.");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") });
        }
    }
}
