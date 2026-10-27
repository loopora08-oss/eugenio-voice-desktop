using System.Globalization;

namespace VoceLocale.Core;

/// <summary>Ricerca letterale nel documento aperto, senza indicizzazione persistente.</summary>
public static class MeetingSearch
{
    public const int MaximumQueryLength = 200;
    private static readonly CompareInfo Comparison = CultureInfo.InvariantCulture.CompareInfo;

    public static IReadOnlyList<int> Find(IReadOnlyList<MeetingEntry> entries, string? query, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (query?.Length > MaximumQueryLength) throw new ArgumentException("Cerca al massimo 200 caratteri.", nameof(query));
        query = query?.Trim();
        if (string.IsNullOrEmpty(query) || Comparison.Compare(query, "", CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0) return [];
        var matches = new List<int>();
        for (int index = 0; index < entries.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            var entry = entries[index];
            if (Contains(entry.Original, query) || Contains(entry.English, query)) matches.Add(index);
        }
        return matches;
    }

    private static bool Contains(string? text, string query) => !string.IsNullOrEmpty(text)
        && Comparison.IndexOf(text, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    public static int Move(int current, int count, int direction)
    {
        if (count <= 0) return -1;
        if (current < 0 || current >= count) return direction < 0 ? count - 1 : 0;
        return (int)(((long)current + (direction < 0 ? -1 : 1) + count) % count);
    }
}
