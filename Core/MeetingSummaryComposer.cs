namespace VoceLocale.Core;

public static class MeetingSummaryComposer
{
    public static async Task<string> GenerateAsync(IReadOnlyList<string> excerpts,
        Func<string, CancellationToken, Task<(bool Ok, string? Text, bool Extractive)>> summarize,
        CancellationToken token)
    {
        if (excerpts.Count == 0) throw new InvalidDataException("Nessun contenuto da riassumere.");
        var output = new List<string>();
        foreach (var excerpt in excerpts)
        {
            token.ThrowIfCancellationRequested();
            var part = await summarize(excerpt, token);
            if (!part.Ok || part.Extractive || string.IsNullOrWhiteSpace(part.Text))
                throw new InvalidDataException("Un brano non ha prodotto un riepilogo validato.");
            output.Add(part.Text);
        }
        token.ThrowIfCancellationRequested();
        return string.Join("\n\n", output);
    }
}
