using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using VoceLocale.Core;

namespace VoceLocale;

public partial class MeetingWindow : Window
{
    public sealed record Caption(string Time, string Original, string English, string Warning, long Sequence);
    private readonly Func<Settings> _settings;
    private readonly Func<bool> _acquireMicrophone;
    private readonly Action _releaseMicrophone;
    private readonly EngineClient _whisper = new("WhisperWorker", realtime: true);
    private readonly OllamaTextService _text = new();
    private readonly ObservableCollection<Caption> _captions = [];
    private readonly Stopwatch _time = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private MeetingRecorder? _recorder;
    private MeetingStream? _stream;
    private CancellationTokenSource? _lifetime;
    private MeetingDocument _document = new();
    private bool _paused, _finishing, _starting, _close, _notesBusy;
    private int _notedEntries;
    private long _lastNotesAt;
    private string? _path;
    private volatile bool _translationEnabled;
    private string? _captureFailure;
    private bool _restoring;
    private bool _writingSummary, _summaryEdited;
    private Task? _notesRunning;
    private CancellationTokenSource? _summaryCancellation;
    private Task? _translationsFinishing, _stopRunning, _startRunning;
    private readonly CancellationTokenSource _windowLife = new();
    public bool Active => _starting || _recorder != null;
    public event Action<string, bool, string>? CaptureStateChanged;
    public event Action<double>? InputLevelChanged;
    public event Action? NewDocumentRequested;
    public event Action<MeetingDocument, string>? DocumentOpenRequested;
    public MeetingWindow(Func<Settings> settings, Func<bool> acquire, Action release)
    {
        InitializeComponent(); _settings = settings; _acquireMicrophone = acquire; _releaseMicrophone = release;
        TranscriptList.ItemsSource = _captions;
        NotesBox.TextChanged += (_, _) =>
        {
            if (!_restoring && SummaryBox.Text.Length > 0)
            { _document.SummaryNeedsUpdate = true; NotesHint.Text = "Appunti modificati: genera di nuovo il riepilogo per includerli."; }
        };
        SummaryBox.TextChanged += (_, _) => { if (!_restoring && !_writingSummary) _summaryEdited = true; };
        TranslationBox.Checked += (_, _) => _translationEnabled = true;
        TranslationBox.Unchecked += (_, _) => _translationEnabled = false;
        MicrophoneBox.ItemsSource = Recorder.Microphones();
        SpeechProfileBox.SelectedIndex = settings().TranscriptionProfile switch { "accurate" => 2, "speed" => 1, _ => 0 };
        MeetingLanguageBox.SelectedIndex = settings().Language switch { "it" => 1, "en" => 2, _ => 0 };
        int index = Array.IndexOf(Recorder.Microphones(), settings().Microphone); MicrophoneBox.SelectedIndex = Math.Max(0, index);
        _clock.Tick += (_, _) =>
        {
            TimerLabel.Text = _time.Elapsed.ToString(@"hh\:mm\:ss");
            if (Active && !_paused && !_finishing)
            {
                CaptureStateChanged?.Invoke($"Meeting {_time.Elapsed:hh\\:mm\\:ss} · {(_stream?.PendingChunks ?? 0)} in coda", true, "recording");
                if (AutomaticNotesBox.IsChecked == true && _document.Entries.Count - _notedEntries >= 6 && Environment.TickCount64 - _lastNotesAt >= 60000)
                    _ = GenerateNotesAsync(automatic: true);
            }
        };
        Closing += (_, e) => { if (!_close) { e.Cancel = true; Hide(); } };
    }
    private async void StartMeeting(object sender, RoutedEventArgs e)
    {
        if (_starting) return;
        _startRunning = StartMeetingAsync();
        await _startRunning;
    }
    private async Task StartMeetingAsync()
    {
        if (Active || _document.Entries.Count > 0)
        {
            StateLabel.Text = Active ? "Il meeting è già attivo." : "Usa Nuovo per un altro meeting. Questo documento rimane aperto.";
            return;
        }
        if (MicrophoneBox.SelectedIndex < 0) { StateLabel.Text = "Seleziona un microfono disponibile."; return; }
        if (RecordingConsentBox.IsChecked != true) { StateLabel.Text = "Prima di registrare, informa i partecipanti e conferma il loro consenso."; return; }
        if (!_acquireMicrophone()) { StateLabel.Text = "Termina prima la dettatura o l’elaborazione in corso."; return; }
        _starting = true; StartButton.IsEnabled = NewButton.IsEnabled = false; MicrophoneBox.IsEnabled = false;
        _captureFailure = null;
        SpeechProfileBox.IsEnabled = MeetingLanguageBox.IsEnabled = SystemAudioBox.IsEnabled = RecordingConsentBox.IsEnabled = false;
        StopButton.IsEnabled = true;
        StateLabel.Text = "Preparazione del meeting…";
        CaptureStateChanged?.Invoke("Meeting · preparazione, microfono spento", false, "processing");
        _lifetime = new CancellationTokenSource();
        try
        {
            bool baseAvailable = await Storage.VerifyAsync(Models.FastWhisper, Storage.ModelPath(Models.FastWhisper), _lifetime.Token);
            bool smallAvailable = await Storage.VerifyAsync(Models.Whisper, Storage.ModelPath(Models.Whisper), _lifetime.Token);
            if (!baseAvailable && !smallAvailable) throw new InvalidDataException("Whisper mancante: scarica i modelli dalle impostazioni di Eugenio.");
            string profile = ((System.Windows.Controls.ComboBoxItem)SpeechProfileBox.SelectedItem).Tag.ToString()!;
            string language = ((System.Windows.Controls.ComboBoxItem)MeetingLanguageBox.SelectedItem).Tag.ToString()!;
            var lastLanguages = new Dictionary<MeetingAudioSource, string?>();
            var model = SpeechProfile.Choose(profile, 7 * 32000, baseAvailable, smallAvailable, language);
            var loaded = await _whisper.RunAsync(new(Guid.NewGuid().ToString(), "load", Storage.ModelPath(model)), _lifetime.Token);
            if (!loaded.Ok) throw new IOException("Motore meeting non disponibile.");
            var settings = _settings();
            _document = new() { Title = TitleBox.Text, CreatedAt = DateTimeOffset.Now };
            _notedEntries = 0; _lastNotesAt = Environment.TickCount64; _path = null;
            _stream = new MeetingStream(
                async (chunk, token) =>
                {
                    var selected = SpeechProfile.Choose(profile, chunk.Audio.Length, baseAvailable, smallAvailable, language == "auto" ? lastLanguages.GetValueOrDefault(chunk.Source) : language);
                    var result = await _whisper.RunAsync(new(Guid.NewGuid().ToString(), "transcribe", Storage.ModelPath(selected), language, chunk.Audio, Mode: SpeechProfile.DecoderMode(profile), Vocabulary: settings.Vocabulary), token);
                    if (result.Ok) lastLanguages[chunk.Source] = result.Language;
                    return result.Ok ? result with { Provenance = new RecognitionProvenance(selected.FileName, selected.Sha256,
                        RecognitionProvenance.WhisperEngineVersion, SpeechProfile.DecoderMode(profile), language, DateTimeOffset.UtcNow) } : result;
                },
                async (source, language, token) =>
                {
                    var translated = await _text.TranslateAsync(settings.TranslationModel, source, settings.Vocabulary, token, language);
                    return translated.Ok ? translated.Text : null;
                });
            _stream.EntryAdded += entry => Dispatcher.BeginInvoke(new Action(() => AddEntry(entry)));
            _stream.EntryUpdated += entry => Dispatcher.BeginInvoke(new Action(() => UpdateEntry(entry)));
            _stream.QueueChanged += count => Dispatcher.BeginInvoke(new Action(() => FooterLabel.Text = $"{count} blocchi in elaborazione · nessun limite di un’ora · Salva conserva appunti e trascrizione, mai audio"));
            _stream.TranslationQueueChanged += count => Dispatcher.BeginInvoke(new Action(() => FooterLabel.Text = $"{_stream?.PendingChunks ?? 0} blocchi vocali · {count} traduzioni in coda · Salva conserva appunti e trascrizione, mai audio"));
            _stream.Fault += reason => Dispatcher.BeginInvoke(new Action(async () => { StateLabel.Text = reason; await StopAsync(); }));
            _recorder = new MeetingRecorder(SystemAudioBox.IsChecked == true);
            _recorder.ChunkReady += chunk =>
            {
                if (!_stream.Submit(chunk, _translationEnabled)) _ = Dispatcher.BeginInvoke(new Action(async () => { StateLabel.Text = "Elaborazione in ritardo: cattura fermata, recupero dei blocchi in coda."; await StopAsync(); }));
            };
            _recorder.InputLevelChanged += rms => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_recorder == null || _paused || _finishing) return;
                MicrophoneLevel.Value = Math.Clamp(rms / 500, 0, 1); LevelLabel.Text = rms >= AudioSignal.MinimumSpeechRms ? "Ti sento" : "In ascolto";
                InputLevelChanged?.Invoke(rms);
            }));
            _recorder.DeviceFailed += () => Dispatcher.BeginInvoke(new Action(async () =>
            {
                _captureFailure = "Una sorgente audio è diventata indisponibile. Controlla microfono e uscita PC; verifica gli ultimi intervalli prima di riprovare.";
                StateLabel.Text = _captureFailure;
                await StopAsync();
                StateLabel.Text = _captureFailure;
            }));
            _recorder.Start(MicrophoneBox.SelectedIndex);
            _time.Restart(); _clock.Start(); _paused = false; _starting = false;
            PauseButton.IsEnabled = StopButton.IsEnabled = true;
            StateLabel.Text = "● Meeting in ascolto · trascrizione live";
            CaptureStateChanged?.Invoke("Meeting · registrazione", true, "recording");
        }
        catch (Exception ex)
        {
            StateLabel.Text = ex is OperationCanceledException ? "Avvio annullato · microfono spento" : ex is InvalidDataException ? ex.Message : "Meeting non avviato. Verifica microfono e modelli locali.";
            _starting = false; _recorder?.Dispose(); _recorder = null; _stream?.Dispose(); _stream = null;
            PauseButton.IsEnabled = StopButton.IsEnabled = false;
            _lifetime?.Dispose(); _lifetime = null; StartButton.IsEnabled = NewButton.IsEnabled = MicrophoneBox.IsEnabled = true; _releaseMicrophone();
            SpeechProfileBox.IsEnabled = MeetingLanguageBox.IsEnabled = SystemAudioBox.IsEnabled = RecordingConsentBox.IsEnabled = true;
        }
    }
    private void AddEntry(MeetingEntry entry)
    {
        _document.Entries.Add(entry); _captions.Add(ToCaption(entry));
        if (SummaryBox.Text.Length > 0) _document.SummaryNeedsUpdate = true;
        if (string.IsNullOrWhiteSpace(SearchBox.Text)) TranscriptList.ScrollIntoView(_captions.Last());
        CountLabel.Text = $"{_captions.Count} blocchi";
        ScheduleSearch(false);
    }
    private void UpdateEntry(MeetingEntry entry)
    {
        int index = _document.Entries.FindIndex(x => x.Sequence == entry.Sequence);
        if (index < 0) return;
        _document.Entries[index] = entry; _captions[index] = ToCaption(entry);
        ScheduleSearch(false);
    }
    private static Caption ToCaption(MeetingEntry entry) => new(
        TimeSpan.FromSeconds(entry.StartSeconds).ToString(@"hh\:mm\:ss") + (entry.Source == MeetingAudioSource.System ? " · Audio PC · parlante non identificato" : " · Microfono"), entry.Original,
        entry.English ?? (entry.TranslationRequested && entry.Warning == null ? "Traduzione in corso…" : ""), entry.Warning ?? "", entry.Sequence);
    private async void PauseMeeting(object sender, RoutedEventArgs e)
    {
        if (_recorder == null || _finishing) return;
        PauseButton.IsEnabled = false;
        try
        {
            if (!_paused)
            {
                await _recorder.StopAsync();
                if (_finishing || _close) return;
                _paused = true;
                StateLabel.Text = "In pausa · microfono spento"; PauseButton.Content = "Riprendi";
                MicrophoneLevel.Value = 0; InputLevelChanged?.Invoke(0); LevelLabel.Text = "Microfono spento"; CaptureStateChanged?.Invoke("Meeting in pausa · microfono spento", false, "paused");
            }
            else
            {
                _recorder.Start(MicrophoneBox.SelectedIndex); _paused = false; PauseButton.Content = "Pausa";
                StateLabel.Text = "● Meeting in ascolto · trascrizione live"; CaptureStateChanged?.Invoke("Meeting · registrazione", true, "recording");
            }
        }
        catch { StateLabel.Text = "Microfono non disponibile. Gli appunti sono conservati."; await StopAsync(); }
        finally { PauseButton.IsEnabled = _recorder != null && !_finishing; }
    }
    private async void StopMeeting(object sender, RoutedEventArgs e) => await StopAsync();
    public Task StopAsync()
    {
        if (_starting) { _lifetime?.Cancel(); return _startRunning ?? Task.CompletedTask; }
        return _finishing ? _stopRunning ?? Task.CompletedTask : _stopRunning = StopCoreAsync();
    }
    private async Task StopCoreAsync()
    {
        if (_recorder == null || _finishing) return;
        _finishing = true; PauseButton.IsEnabled = StopButton.IsEnabled = false;
        _time.Stop();
        StateLabel.Text = "Completamento dei blocchi · arresto del microfono";
        CaptureStateChanged?.Invoke("Meeting · completamento dei blocchi, microfono spento", false, "processing");
        try
        {
            await _recorder.StopAsync();
            MicrophoneLevel.Value = 0; InputLevelChanged?.Invoke(0); LevelLabel.Text = "Microfono spento";
            StateLabel.Text = "Microfono spento · completamento della trascrizione";
            if (_stream != null) await _stream.CompleteCaptureAsync();
            // Entry events were dispatched asynchronously; allow them to finish before summarizing/saving.
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
            _document.DurationSeconds = _time.Elapsed.TotalSeconds;
            if (AutomaticNotesBox.IsChecked == true)
            {
                if (_notesRunning != null) await _notesRunning;
                await GenerateNotesAsync(automatic: true);
            }
            StateLabel.Text = _captureFailure ?? (_stream?.PendingTranslations > 0 ? "Meeting terminato · inglese ancora in elaborazione" : "Meeting terminato · appunti e trascrizione disponibili");
        }
        catch { StateLabel.Text = "Meeting terminato con un errore. Verifica gli intervalli segnalati."; }
        finally
        {
            _time.Stop(); _clock.Stop(); _recorder.Dispose(); _recorder = null;
            if (_stream is { } finished) _translationsFinishing = FinishTranslationsAsync(finished);
            _lifetime?.Cancel(); _lifetime?.Dispose(); _lifetime = null;
            _finishing = _paused = false; MicrophoneLevel.Value = 0; InputLevelChanged?.Invoke(0); LevelLabel.Text = "Microfono spento";
            StartButton.IsEnabled = NewButton.IsEnabled = MicrophoneBox.IsEnabled = true; _whisper.Stop(); _releaseMicrophone();
            SpeechProfileBox.IsEnabled = MeetingLanguageBox.IsEnabled = SystemAudioBox.IsEnabled = RecordingConsentBox.IsEnabled = true;
        }
    }
    private async Task FinishTranslationsAsync(MeetingStream stream)
    {
        await stream.TranslationCompletion;
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (!_close && !_windowLife.IsCancellationRequested) { FooterLabel.Text = "Elaborazione completata · Salva per conservare anche le traduzioni aggiunte"; if (_recorder == null && !_starting) StateLabel.Text = _captureFailure ?? "Meeting terminato · appunti e trascrizione disponibili"; }
        stream.Dispose(); if (ReferenceEquals(_stream, stream)) _stream = null;
    }
    private async void GenerateNotes(object sender, RoutedEventArgs e)
    {
        if (_notesBusy) { _summaryCancellation?.Cancel(); return; }
        await GenerateNotesAsync();
    }
    private Task GenerateNotesAsync(bool automatic = false)
    {
        if (automatic && _summaryEdited)
        {
            NotesHint.Text = "Riepilogo modificato a mano: aggiornamento automatico sospeso. Genera riepilogo sostituisce il testo con una nuova versione.";
            return Task.CompletedTask;
        }
        if (_notesBusy) return _notesRunning ?? Task.CompletedTask;
        return _notesRunning = GenerateNotesCoreAsync();
    }
    private async Task GenerateNotesCoreAsync()
    {
        if (_notesBusy) return;
        var groups = MeetingSummaryInput.Build(_document.Entries, NotesBox.Text);
        if (groups.Count == 0) { NotesHint.Text = "Scrivi appunti o acquisisci una trascrizione prima di generare il riepilogo."; return; }
        _notesBusy = true; NotesButton.Content = "Annulla riepilogo"; _lastNotesAt = Environment.TickCount64;
        using var summaryCancellation = CancellationTokenSource.CreateLinkedTokenSource(_windowLife.Token);
        summaryCancellation.CancelAfter(TimeSpan.FromMinutes(10)); _summaryCancellation = summaryCancellation;
        SummaryBox.IsReadOnly = true;
        int count = _document.Entries.Count;
        string personalNotes = NotesBox.Text;
        try
        {
            NotesHint.Text = "Generazione del riepilogo locale… puoi continuare a scrivere negli appunti personali.";
            string generated = await MeetingSummaryComposer.GenerateAsync(groups, async (excerpt, token) =>
            {
                var result = await _text.NotesAsync(_settings().TextModel, excerpt, token);
                return (result.Ok, result.Text, result.ExtractiveFallback);
            }, summaryCancellation.Token);
            _writingSummary = true;
            try { SummaryBox.Text = generated; }
            finally { _writingSummary = false; }
            _summaryEdited = false;
            _notedEntries = count;
            _document.SummaryNeedsUpdate = count != _document.Entries.Count || personalNotes != NotesBox.Text;
            NotesTabs.SelectedIndex = 1;
            NotesHint.Text = _document.SummaryNeedsUpdate ? "Riepilogo pronto sui dati precedenti: aggiorna per includere le ultime modifiche."
                : "Riepilogo pronto. Verifica decisioni, incarichi, nomi e cifre prima di condividerlo.";
        }
        catch (OperationCanceledException) { NotesHint.Text = "Generazione annullata o tempo massimo raggiunto. Appunti e riepilogo precedente sono conservati."; }
        catch { NotesHint.Text = "Riepilogo non disponibile o non validato. Appunti, trascrizione e riepilogo precedente sono conservati; controlla il modello locale e riprova."; }
        finally { _summaryCancellation = null; _notesBusy = false; NotesButton.Content = "Genera riepilogo"; SummaryBox.IsReadOnly = false; }
    }
    private void SaveDocument(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Salva appunti Eugenio", FileName = _path == null ? $"Eugenio-{_document.CreatedAt:yyyy-MM-dd-HHmm}" : Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(_path)), Filter = "Appunti Eugenio|*.eugenio.json|Markdown|*.md", DefaultExt = ".eugenio.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _document.Title = TitleBox.Text; _document.Notes = NotesBox.Text; _document.Summary = SummaryBox.Text; _document.SummaryEdited = _summaryEdited; _document.NotedEntries = _notedEntries;
            if (Active) _document.DurationSeconds = _time.Elapsed.TotalSeconds;
            string data = dialog.FilterIndex == 2 ? _document.Markdown() : _document.Json();
            string path = dialog.FileName;
            if (dialog.FilterIndex == 2 && !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) path = path.EndsWith(".eugenio.json", StringComparison.OrdinalIgnoreCase) ? path[..^13] + ".md" : Path.ChangeExtension(path, ".md");
            if (!path.Equals(dialog.FileName, StringComparison.OrdinalIgnoreCase) && File.Exists(path)
                && MessageBox.Show(this, "Il file Markdown esiste già. Sovrascriverlo?", "Salva appunti Eugenio", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temporary, data); File.Move(temporary, path, true); }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
            _path = path; FooterLabel.Text = "Salvato sul PC · il file contiene appunti e trascrizione, nessun audio";
        }
        catch { FooterLabel.Text = "Salvataggio non riuscito. Gli appunti restano aperti: scegli un'altra cartella."; }
    }
    private void OpenDocument(object sender, RoutedEventArgs e)
    {
        if (Active) { StateLabel.Text = "Termina il meeting prima di aprire altri appunti."; return; }
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Appunti Eugenio|*.eugenio.json", Title = "Apri appunti Eugenio" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 32 * 1024 * 1024) throw new InvalidDataException();
            var doc = JsonSerializer.Deserialize<MeetingDocument>(File.ReadAllText(dialog.FileName)) ?? throw new InvalidDataException();
            if (!doc.IsValid()) throw new InvalidDataException();
            if (_document.Entries.Count > 0 || NotesBox.Text.Length > 0 || SummaryBox.Text.Length > 0) DocumentOpenRequested?.Invoke(doc, dialog.FileName);
            else RestoreDocument(doc, dialog.FileName);
        }
        catch { StateLabel.Text = "File di appunti non valido: il documento corrente è conservato."; }
    }
    internal void RestoreDocument(MeetingDocument doc, string path)
    {
        if (Active) throw new InvalidOperationException("A running meeting cannot be replaced.");
        if (!doc.IsValid()) throw new InvalidDataException("Invalid meeting document.");
        _document = doc; _path = path; _captions.Clear(); foreach (var entry in doc.Entries) _captions.Add(ToCaption(entry));
        _restoring = true;
        try { NotesBox.Text = doc.Notes; SummaryBox.Text = doc.Summary; }
        finally { _restoring = false; }
        _summaryEdited = doc.SummaryEdited;
        TitleBox.Text = doc.Title; TimerLabel.Text = TimeSpan.FromSeconds(doc.DurationSeconds).ToString(@"hh\:mm\:ss");
        if (doc.SummaryNeedsUpdate) NotesHint.Text = "Il riepilogo salvato va aggiornato con gli ultimi appunti o blocchi.";
        _notedEntries = doc.NotedEntries ?? (string.IsNullOrWhiteSpace(doc.Notes) ? 0 : doc.Entries.Count); CountLabel.Text = $"{_captions.Count} blocchi"; StateLabel.Text = "Appunti aperti · microfono spento";
        ScheduleSearch(true);
    }
    private void NewDocument(object sender, RoutedEventArgs e) { if (!Active) NewDocumentRequested?.Invoke(); }
    public async Task ShutdownAsync()
    {
        _windowLife.Cancel(); _lifetime?.Cancel(); await StopAsync();
        _searchCancellation?.Cancel();
        if (_searchRunning != null) await _searchRunning;
        _stream?.Dispose();
        if (_translationsFinishing != null) await _translationsFinishing;
        if (_notesRunning != null) await _notesRunning;
        _close = true; _clock.Stop(); _windowLife.Dispose();
        _whisper.Dispose(); _text.Dispose(); Close();
    }
}
