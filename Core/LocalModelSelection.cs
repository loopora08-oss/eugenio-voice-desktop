namespace VoceLocale.Core;

/// <summary>Seleziona soltanto il modello richiesto; nessuna sostituzione tra progetti.</summary>
public static class LocalModelSelection
{
    public static string? FindConfigured(IReadOnlyList<string> installed, string? configured) =>
        string.IsNullOrWhiteSpace(configured) ? null :
        installed.FirstOrDefault(name => name.Equals(configured, StringComparison.OrdinalIgnoreCase));
}
