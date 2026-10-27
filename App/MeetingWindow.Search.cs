using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VoceLocale.Core;

namespace VoceLocale;

public partial class MeetingWindow
{
    private CancellationTokenSource? _searchCancellation;
    private Task? _searchRunning;
    private IReadOnlyList<int> _searchMatches = [];
    private int _searchPosition = -1;
    private long _searchGeneration;

    private void SearchChanged(object sender, TextChangedEventArgs e) => ScheduleSearch(true);

    private void ScheduleSearch(bool resetPosition)
    {
        _searchCancellation?.Cancel();
        long generation = ++_searchGeneration;
        string query = SearchBox.Text;
        if (resetPosition) TranscriptList.SelectedIndex = -1;
        SearchPreviousButton.IsEnabled = SearchNextButton.IsEnabled = false;
        SearchClearButton.IsEnabled = query.Length > 0;
        if (string.IsNullOrWhiteSpace(query))
        {
            _searchMatches = []; _searchPosition = -1;
            SearchStatus.Text = _captions.Count == 0 ? "Nessuna trascrizione da cercare." : "Originale e inglese · Ctrl+F per cercare";
            return;
        }
        int priorEntry = !resetPosition && _searchPosition >= 0 && _searchPosition < _searchMatches.Count
            ? _searchMatches[_searchPosition] : -1;
        SearchStatus.Text = "Ricerca in corso…";
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_windowLife.Token);
        _searchCancellation = cancellation;
        _searchRunning = SearchAsync(query, generation, priorEntry, cancellation);
    }

    private async Task SearchAsync(string query, long generation, int priorEntry, CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(180, cancellation.Token);
            // Snapshot sul dispatcher: la cattura e le traduzioni possono aggiornare il documento.
            var entries = _document.Entries.ToArray();
            var matches = await Task.Run(() => MeetingSearch.Find(entries, query, cancellation.Token), cancellation.Token);
            if (generation != _searchGeneration || cancellation.IsCancellationRequested) return;
            _searchMatches = matches;
            _searchPosition = matches.ToList().IndexOf(priorEntry);
            if (_searchPosition < 0 && matches.Count > 0) _searchPosition = 0;
            SearchPreviousButton.IsEnabled = SearchNextButton.IsEnabled = matches.Count > 0;
            if (matches.Count == 0)
            {
                TranscriptList.SelectedIndex = -1;
                SearchStatus.Text = "Nessun blocco corrispondente. Prova altre parole.";
            }
            else ShowSearchMatch();
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (generation == _searchGeneration)
            {
                _searchMatches = []; _searchPosition = -1;
                SearchStatus.Text = "Ricerca non riuscita. Modifica il testo per riprovare; la trascrizione è conservata.";
            }
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation)) _searchCancellation = null;
            cancellation.Dispose();
        }
    }

    private void ShowSearchMatch()
    {
        if (_searchPosition < 0 || _searchPosition >= _searchMatches.Count) return;
        int index = _searchMatches[_searchPosition];
        if (index < 0 || index >= _captions.Count) return;
        TranscriptList.SelectedIndex = index;
        TranscriptList.ScrollIntoView(_captions[index]);
        SearchStatus.Text = $"{_searchPosition + 1} di {_searchMatches.Count} blocchi · originale e inglese";
    }

    private void SearchPrevious(object sender, RoutedEventArgs e) => MoveSearch(-1);
    private void SearchNext(object sender, RoutedEventArgs e) => MoveSearch(1);
    private void MoveSearch(int direction)
    {
        if (_searchCancellation != null) return;
        _searchPosition = MeetingSearch.Move(_searchPosition, _searchMatches.Count, direction);
        ShowSearchMatch();
    }
    private void ClearSearch(object sender, RoutedEventArgs e) { SearchBox.Clear(); SearchBox.Focus(); }
    private void SearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { MoveSearch(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1); e.Handled = true; }
        else if (e.Key == Key.Escape) { SearchBox.Clear(); e.Handled = true; }
    }
    private void MeetingKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        { NotesTabs.SelectedIndex = 1; SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
    }
}
