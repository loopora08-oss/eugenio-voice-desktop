using System.Globalization;

namespace VoceLocale.Core;

public static class MeetingSummaryInput
{
    public static IReadOnlyList<string> Build(IReadOnlyList<MeetingEntry> entries, string notes, int limit = 6000)
    {
        if (limit < 256 || limit > 8000) throw new ArgumentOutOfRangeException(nameof(limit));
        var parts = entries.Where(entry => !string.IsNullOrWhiteSpace(entry.Original))
            .Select(entry => $"[{TimeSpan.FromSeconds(entry.StartSeconds).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)}] {entry.Original}").ToList();
        if (!string.IsNullOrWhiteSpace(notes)) parts.Add("appunti personali:\n" + notes);
        var groups = new List<string>(); string group = "";
        foreach (string part in parts)
        {
            string remaining = part;
            while (remaining.Length > 0)
            {
                int available = limit - group.Length - (group.Length > 0 ? 1 : 0);
                if (available == 0) { groups.Add(group); group = ""; continue; }
                if (remaining.Length > available && group.Length > 0) { groups.Add(group); group = ""; continue; }
                int take = Math.Min(available, remaining.Length);
                if (take < remaining.Length)
                {
                    int boundary = remaining.LastIndexOf(' ', take - 1, take);
                    if (boundary >= take / 2) take = boundary + 1;
                }
                group += (group.Length > 0 ? "\n" : "") + remaining[..take];
                remaining = remaining[take..];
                if (remaining.Length > 0) { groups.Add(group); group = ""; }
            }
        }
        if (group.Length > 0) groups.Add(group);
        return groups;
    }
}
