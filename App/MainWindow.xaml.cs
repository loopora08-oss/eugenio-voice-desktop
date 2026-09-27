using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Threading.Channels;
using VoceLocale.Core;
using Forms = System.Windows.Forms;

namespace VoceLocale;

public partial class MainWindow : Window
{
    private readonly bool _previewOnly;
    private Settings _settings = Storage.LoadSettings();
    private readonly EngineClient _whisper = new("WhisperWorker", realtime: true);
    private readonly EngineClient _wakeWhisper = new("WhisperWorker");
    private ModelSpec _dictationModel = Models.Whisper, _wakeModel = Models.Whisper;
    private bool _baseAvailable, _smallAvailable;
    private string? _lastSpeechLanguage;
    private readonly OllamaRewriter _rewrite = new();
    private readonly OllamaTextService _translation = new();
    private readonly List<MeetingWindow> _meetings = [];
    private bool _meetingHeld;
    private readonly Recorder _recorder = new();
    private readonly BadgeWindow _badge;
    private readonly VoiceWakeListener _wake = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Stopwatch _recordTime = new();
    private DesktopInput? _desktop;
    private Forms.NotifyIcon? _tray;
    private CancellationTokenSource? _session;
    private CancellationTokenSource? _setup;
    private InputTarget? _target;
    private string _pending = "";
    private Channel<byte[]>? _segmentChannel;
    private Task? _segmentWorker;
    private int _segmentsEmitted, _insertedBlocks;
    private string _segmentFailureText = "";
    private string _segmentFailureReason = "";
    private bool _ready, _busy, _recording, _closing, _recoveryArmed, _manualOnly, _hotkeyAvailable, _wakeListening, _initialLoadStarted;
    private string _shortcutNotice = "";

    public MainWindow(bool previewOnly = false)
    {
        _previewOnly = previewOnly;
        InitializeComponent();
        _badge = new BadgeWindow(ShowSettings, ToggleAsync);
        _badge.RestorePosition(_settings.BadgeLeft, _settings.BadgeTop);
        _badge.PositionChanged += (left, top) =>
        {
            _settings.BadgeLeft = left; _settings.BadgeTop = top;
            try { Storage.SaveSettings(_settings); } catch { }
        };
        ShortcutBox.Text = _settings.Shortcut;
        LanguageBox.SelectedIndex = _settings.Language switch { "it" => 1, "en" => 2, _ => 0 };
        SpeedBox.SelectedIndex = _settings.TranscriptionProfile switch { "accurate" => 2, "speed" => 1, _ => 0 };
        TranslateBox.IsChecked = _settings.TranslateToEnglish;
        ModeBox.SelectedIndex = _settings.Mode switch { "message" => 1, "email" => 2, "code" => 3, _ => 0 };
        RewriteModelBox.Text = _settings.RewriteModel;
        TextModelBox.Text = _settings.TextModel;
        VocabularyBox.Text = string.Join(Environment.NewLine, _settings.Vocabulary ?? []);
        RefreshMicrophoneList();
        _recorder.LimitReached += () => Dispatcher.BeginInvoke(new Action(async () => await StopRecordingAsync()));
        _recorder.SegmentReady += OnSegmentReady;
        _recorder.InputLevelChanged += OnInputLevel;
        _wake.InputLevelChanged += OnInputLevel;
        _recorder.VoiceActivityChanged += detected => Dispatcher.BeginInvoke(new Action(() => _badge.SetVoiceDetected(detected)));
        _recorder.DeviceFailed += () => Dispatcher.BeginInvoke(new Action(() => { CancelSession(); Status("Microfono non disponibile", "Ricollega il microfono e riprova."); }));
        RefreshRewriteModelsButton.Click += async (_, _) => await RefreshRewriteModelsAsync();
        _wake.VoiceActivityChanged += detected => Dispatcher.BeginInvoke(new Action(() => _badge.SetVoiceDetected(detected)));
        _wake.Failed += () => Dispatcher.BeginInvoke(new Action(() => { _wakeListening = false; _badge.SetListening(false); _badge.SetStatus("Microfono non disponibile", "error"); }));
        _clock.Tick += (_, _) =>
        {
            if (_recording) _badge.SetStatus($"Registrazione {_recordTime.Elapsed:mm\\:ss} / 02:00 · clicca per fermare", "recording");
            if (_recording && _recordTime.Elapsed >= TimeSpan.FromSeconds(120)) _ = StopRecordingAsync();
        };
        SourceInitialized += (_, _) => InitializeDesktop();
        Loaded += async (_, _) =>
        {
            if (_initialLoadStarted) return;
            _initialLoadStarted = true;
            if (_previewOnly)
            {
                SettingsPanel.IsEnabled = ModelPanel.IsEnabled = false;
                Status("Anteprima dell’interfaccia", "Microfono e scorciatoie disattivati. La sessione già aperta rimane disponibile.");
                return;
            }
            Hide();
            _badge.Show();
            await VerifyModelsAsync();
        };
        Closing += OnClosing;
    }
    private void InitializeDesktop()
    {
        if (_previewOnly) return;
        try
        {
            var badgeHandle = new WindowInteropHelper(_badge).EnsureHandle();
            _desktop = new DesktopInput(new WindowInteropHelper(this).Handle, badgeHandle);
            _desktop.Toggle += () => Dispatcher.BeginInvoke(new Action(async () => await ToggleAsync()));
            _desktop.Cancel += () => Dispatcher.BeginInvoke(new Action(CancelSession));
            _hotkeyAvailable = Hotkey.TryParse(_settings.Shortcut, out var key) && _desktop.Configure(key);
            if (!_hotkeyAvailable && _settings.Shortcut == "Ctrl+Alt+Space" && !File.Exists(Storage.SettingsPath))
            {
                Hotkey.TryParse("Ctrl+Alt+F9", out var fallback);
                if (_desktop.Configure(fallback))
                {
                    _hotkeyAvailable = true;
                    _settings.Shortcut = "Ctrl+Alt+F9";
                    ShortcutBox.Text = _settings.Shortcut;
                    _shortcutNotice = "Ctrl+Alt+Spazio è occupata: uso Ctrl+Alt+F9. Puoi modificarla nelle impostazioni. ";
                    Storage.SaveSettings(_settings);
                }
            }
            if (!_hotkeyAvailable) Status("Scorciatoia non disponibile", "Scegli una combinazione diversa e salva le impostazioni.");
        }
        catch { Status("Controlli globali non disponibili", "Chiudi e riapri l’app nella sessione desktop normale."); }
        _tray = new Forms.NotifyIcon
        {
            Text = "VoceLocale · dettatura locale",
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            Visible = true, ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        _tray.DoubleClick += (_, _) => ShowSettings();
        _tray.ContextMenuStrip.Items.Add("Apri VoceLocale", null, (_, _) => ShowSettings());
        _tray.ContextMenuStrip.Items.Add("Eugenio Notetaker", null, (_, _) => ShowMeeting());
        _tray.ContextMenuStrip.Items.Add("Annulla dettatura", null, (_, _) => CancelSession());
        _tray.ContextMenuStrip.Items.Add("Esci", null, (_, _) => Exit());
    }
    private void OnInputLevel(double rms) => Dispatcher.BeginInvoke(new Action(() =>
    {
        _badge.SetInputLevel(rms);
        InputLevelLabel.Text = rms >= AudioSignal.MinimumSpeechRms ? "Microfono: segnale vocale ricevuto"
            : rms > 10 ? "Microfono: segnale debole · avvicinati o verifica il microfono selezionato"
            : "Microfono: silenzio";
    }));
    private void Status(string title, string detail, bool error = false)
    {
        StatusLabel.Text = title; StatusDetail.Text = detail;
        ShortcutHint.Text = $"{_settings.Shortcut} · avvio / termina     Esc · annulla";
        var state = _recording ? "recording" : _busy ? "processing" : error || title.Contains("non ", StringComparison.OrdinalIgnoreCase) ? "error" : "ready";
        if (_recording) _badge.SetStatus("Registrazione · clicca per fermare", "recording");
        else if (_busy) _badge.SetStatus(title, "processing");
        else if (_pending.Length > 0) _badge.SetStatus("Testo pronto · apri impostazioni", "error");
        else if (state == "error") _badge.SetStatus(title, "error");
        else if (_wakeListening) _badge.SetStatus("In ascolto · dì Eugenio");
        else _badge.SetStatus(title, state);
    }
    internal void ShowSettings() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void OpenNotetaker(object sender, RoutedEventArgs e) => ShowMeeting();
    internal MeetingWindow ShowMeeting(bool fresh = false)
    {
        var window = fresh ? null : _meetings.FirstOrDefault(m => m.Active) ?? _meetings.LastOrDefault();
        if (window == null)
        {
            window = new MeetingWindow(() => _settings, () =>
            {
                if (_previewOnly || _recording || _busy || _setup != null || _pending.Length > 0 || _meetingHeld) return false;
                _meetingHeld = true; _wake.Stop(); _wakeListening = false; _badge.SetListening(false);
                SettingsPanel.IsEnabled = ModelPanel.IsEnabled = false; return true;
            }, () =>
            {
                _meetingHeld = false; _badge.SetSessionActive(false); SettingsPanel.IsEnabled = ModelPanel.IsEnabled = true;
                StartWakeListener();
            });
            window.NewDocumentRequested += () => ShowMeeting(true);
            window.DocumentOpenRequested += (doc, path) => ShowMeeting(true).RestoreDocument(doc, path);
            window.InputLevelChanged += rms => _badge.SetInputLevel(rms);
            window.CaptureStateChanged += (text, recording, state) => { _badge.SetSessionActive(recording); _badge.SetStatus(text, state); };
            _meetings.Add(window);
            var savedWindow = window;
            _tray?.ContextMenuStrip?.Items.Add($"Notetaker {_meetings.Count}", null, (_, _) => { savedWindow.Show(); savedWindow.Activate(); });
        }
        window.Show(); window.WindowState = WindowState.Normal; window.Activate();
        return window;
    }
    private void StartWakeListener()
    {
        if (!_ready || _closing || _recording || _busy || _setup != null || _wakeListening || _pending.Length > 0 || _meetingHeld) return;
        int device = MicrophoneBox.SelectedIndex;
        if (device < 0) { _badge.SetStatus("Seleziona un microfono", "error"); return; }
        try
        {
            _wake.Start(device, RecognizeWakeSegmentAsync);
            _wakeListening = true;
            WakeStatusLabel.Text = "Ascolto del comando Eugenio · rilevamento locale in italiano";
            _badge.SetStatus("Pronto · dì «Eugenio»");
            _badge.SetListening(true);
            Status("In ascolto · solo sul PC", _shortcutNotice + "Dì Eugenio per iniziare. Clicca il badge o usa la scorciatoia per fermare.");
        }
        catch { _wakeListening = false; _badge.SetListening(false); Status("Microfono non disponibile", "Non riesco ad aprire il microfono selezionato.", error: true); }
    }
    private async Task RecognizeWakeSegmentAsync(byte[] audio, CancellationToken token)
    {
        try
        {
            await Dispatcher.InvokeAsync(() => WakeStatusLabel.Text = "Controllo della parola Eugenio…");
            var result = await _wakeWhisper.RunAsync(new(Guid.NewGuid().ToString(), "wake",
                Storage.ModelPath(_wakeModel), "it", audio), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (!result.Ok || string.IsNullOrWhiteSpace(result.Text) || !TextPolicy.IsWakePhrase(result.Text))
            {
                await Dispatcher.InvokeAsync(() => WakeStatusLabel.Text = $"Comando non riconosciuto · ultimo controllo {result.Milliseconds / 1000d:F1} s · riprova Eugenio");
                return;
            }
            await Dispatcher.InvokeAsync(() => WakeStatusLabel.Text = $"Eugenio riconosciuto in {result.Milliseconds / 1000d:F1} s · avvio dettatura");
            await Dispatcher.InvokeAsync(async () => await BeginWakeDictationAsync(audio)).Task.Unwrap().ConfigureAwait(false);
        }
        finally { Array.Clear(audio); }
    }
    private async Task BeginWakeDictationAsync(byte[] audio)
    {
        // A wake result may already be queued when a meeting acquires the microphone.
        if (!_ready || _desktop == null || _closing || _recording || _busy || _setup != null || _meetingHeld || _pending.Length > 0) return;
        byte[] continuation = [];
        try
        {
            _busy = true;
            _session = new CancellationTokenSource();
            _desktop.BeginWatch();
            _target = await Task.Run(DesktopInput.Capture).WaitAsync(TimeSpan.FromSeconds(2), _session.Token);
            if (_target == null)
            {
                _wake.Stop(); _wakeListening = false; _badge.SetListening(false);
                CancelSession();
                Status("Seleziona un campo scrivibile", "La registrazione vocale non è iniziata perché non trovo un campo compatibile.", error: true);
                return;
            }
            int device = MicrophoneBox.SelectedIndex;
            if (device < 0) throw new IOException();
            _desktop.TrackTarget(_target);
            continuation = _wake.StopAndTakeProcessingAudio();
            _wakeListening = false;
            _badge.SetListening(false);
            BeginSegmentPipeline(_session.Token);
            byte[] preRoll = new byte[audio.Length + continuation.Length];
            try
            {
                audio.CopyTo(preRoll, 0);
                continuation.CopyTo(preRoll, audio.Length);
                _recorder.Start(device, preRoll);
            }
            finally { Array.Clear(preRoll); }
            _recording = true; _busy = false;
            _badge.SetSessionActive(true);
            SettingsPanel.IsEnabled = ModelPanel.IsEnabled = false;
            _recordTime.Restart(); _clock.Start();
            Status("Registrazione · clicca il badge per fermare", "Attivata da Eugenio. Il testo verrà inserito senza inviare il messaggio.");
        }
        catch (OperationCanceledException) { }
        catch
        {
            _wake.Stop(); _wakeListening = false; _badge.SetListening(false);
            CancelSession();
            Status("Registrazione non avviata", "Controlla il microfono e il permesso di accesso nelle impostazioni di Windows.");
        }
        finally { Array.Clear(continuation); }
    }
    private static string StripWakePhrase(string text) => TextPolicy.StripWakePhrase(text);
    private void RefreshMicrophoneList()
    {
        try
        {
            var devices = Recorder.Microphones();
            MicrophoneBox.ItemsSource = devices;
            int selected = Array.IndexOf(devices, _settings.Microphone);
            MicrophoneBox.SelectedIndex = selected >= 0 ? selected : devices.Length > 0 ? 0 : -1;
        }
        catch { MicrophoneBox.ItemsSource = Array.Empty<string>(); }
    }
    private void RefreshMicrophones(object sender, RoutedEventArgs e) => RefreshMicrophoneList();
    private async void SaveSettings(object sender, RoutedEventArgs e)
    {
        if (_busy || _recording || _setup != null || _meetingHeld) return;
        if (!Hotkey.TryParse(ShortcutBox.Text, out var key))
        { Status("Scorciatoia non valida", "Usa Ctrl o Alt con una lettera, un numero, Space oppure F1–F11."); return; }
        if (_desktop == null || !_desktop.Configure(key))
        { Status("Scorciatoia occupata", "Scegli un’altra combinazione. La precedente resta attiva, se disponibile."); return; }
        _hotkeyAvailable = true;
        _shortcutNotice = "";
        var settings = new Settings
        {
            TranscriptionProfile = SpeedBox.SelectedIndex switch { 2 => "accurate", 1 => "speed", _ => "adaptive" },
            TranslateToEnglish = TranslateBox.IsChecked == true,
            TextModel = TextModelBox.SelectedItem as string ?? _settings.TextModel,
            TranslationModel = _settings.TranslationModel,
            Shortcut = ShortcutBox.Text.Trim(), Microphone = MicrophoneBox.SelectedItem as string ?? "",
            Language = ((ComboBoxItem)LanguageBox.SelectedItem).Tag.ToString()!,
            Mode = (ModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "general",
            RewriteModel = RewriteModelBox.SelectedItem as string ?? RewriteModelBox.Text.Trim(),
            Vocabulary = VocabularyBox.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(term => term.Length <= 60).Distinct(StringComparer.OrdinalIgnoreCase).Take(40).ToArray(),
            BadgeLeft = _settings.BadgeLeft, BadgeTop = _settings.BadgeTop
        };
        try { Storage.SaveSettings(settings); _settings = settings; await VerifyModelsAsync(); }
        catch { Status("Impostazioni non salvate", "Sposta la cartella dell’app in una posizione scrivibile."); }
    }
    private async Task VerifyModelsAsync()
    {
        if (_setup != null) return;
        _wake.Stop(); _wakeListening = false;
        _ready = false;
        using var setup = new CancellationTokenSource(); _setup = setup;
        SetSetupBusy(true);
        try
        {
            ModelsLabel.Text = "Verifica dei modelli Whisper…";
            bool fastValid = await Storage.VerifyAsync(Models.FastWhisper, Storage.ModelPath(Models.FastWhisper), setup.Token);
            bool smallValid = await Storage.VerifyAsync(Models.Whisper, Storage.ModelPath(Models.Whisper), setup.Token);
            _baseAvailable = fastValid; _smallAvailable = smallValid;
            _dictationModel = smallValid ? Models.Whisper : Models.FastWhisper;
            _wakeModel = fastValid ? Models.FastWhisper : _dictationModel;
            bool whisperValid = fastValid || smallValid;
            ModelsLabel.Text = whisperValid ? $"Dettatura: {_dictationModel.FileName} · comando: {_wakeModel.FileName} · verifica Ollama…" : "Whisper mancante · importa o scarica il modello.";
            var ollamaModels = await _rewrite.GetModelsAsync(setup.Token);
            PopulateRewriteModels(ollamaModels);
            bool rewriteValid = ollamaModels.Contains(_settings.RewriteModel, StringComparer.OrdinalIgnoreCase);
            if (!rewriteValid && RewriteModelBox.SelectedItem is string selected)
                rewriteValid = ollamaModels.Contains(selected, StringComparer.OrdinalIgnoreCase);
            ModelsLabel.Text = $"Dettatura {_dictationModel.FileName} · comando {_wakeModel.FileName} · MiniCPM {(rewriteValid ? "pronto" : "non trovato")}";
            if (whisperValid && rewriteValid) await WarmupAsync(setup.Token);
            else if (!whisperValid) Status("Whisper non disponibile", "Importa ggml-small.bin o scaricalo. MiniCPM viene usato da Ollama sul PC.");
            else Status("Modello Ollama non trovato", "Avvia Ollama e seleziona un modello di correzione installato.");
        }
        catch (OperationCanceledException) { Status("Preparazione annullata", "Puoi riprovare quando vuoi."); }
        catch (HttpRequestException) { ModelsLabel.Text = "Ollama non raggiungibile · Whisper resta locale."; Status("Avvia Ollama", "Eugenio cerca Ollama solo su questo PC (127.0.0.1:11434). Poi premi ‘Verifica modelli’."); }
        catch { Status("Motori locali non avviati", "Verifica Whisper, Ollama e gli eventuali blocchi di Windows."); }
        finally { _setup = null; SetSetupBusy(false); StartWakeListener(); }
    }
    private async Task WarmupAsync(CancellationToken token)
    {
        _ready = false;
        Status("Caricamento dei motori", "Il primo avvio può richiedere tempo. Tutto resta sul PC.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var loads = await Task.WhenAll(
            _whisper.RunAsync(new(Guid.NewGuid().ToString(), "load", Storage.ModelPath(_dictationModel)), timeout.Token),
            _wakeWhisper.RunAsync(new(Guid.NewGuid().ToString(), "load", Storage.ModelPath(_wakeModel)), timeout.Token));
        var installed = await _rewrite.GetModelsAsync(timeout.Token);
        string rewriteModel = RewriteModelBox.SelectedItem as string ?? _settings.RewriteModel;
        if (loads.Any(a => !a.Ok) || !installed.Contains(rewriteModel, StringComparer.OrdinalIgnoreCase)) throw new IOException();
        if (_baseAvailable && _smallAvailable)
        {
            var baseLoad = await _whisper.RunAsync(new(Guid.NewGuid().ToString(), "load", Storage.ModelPath(Models.FastWhisper)), timeout.Token);
            if (!baseLoad.Ok) throw new IOException();
        }
        // Prime MiniCPM with a public synthetic greeting; never use the user's text.
        await _rewrite.RewriteAsync(rewriteModel, "Buongiorno.", "general", null, timeout.Token);
        _settings.RewriteModel = rewriteModel;
        _ready = true;
        if (_hotkeyAvailable) Status("Motori pronti", _shortcutNotice + "Avvio del microfono…");
        else Status("Imposta una scorciatoia libera", "I motori sono pronti. La combinazione scelta è occupata: modificala e salva prima di dettare.");
    }
    private async void LoadModels(object sender, RoutedEventArgs e) => await VerifyModelsAsync();
    private async Task RefreshRewriteModelsAsync()
    {
        try
        {
            var models = await _rewrite.GetModelsAsync(CancellationToken.None);
            PopulateRewriteModels(models);
            ModelsLabel.Text = $"Ollama raggiungibile · {models.Count} modelli installati.";
            if (models.Count == 0) Status("Nessun modello Ollama", "Installa MiniCPM in Ollama, poi aggiorna l’elenco.");
        }
        catch (HttpRequestException) { Status("Ollama non raggiungibile", "Avvia Ollama sul PC e riprova."); }
        catch { Status("Elenco Ollama non disponibile", "Controlla che Ollama risponda su 127.0.0.1:11434."); }
    }
    private void PopulateRewriteModels(IReadOnlyList<string> models)
    {
        var preferred = models.FirstOrDefault(m => m.Equals(_settings.RewriteModel, StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault(m => m.Equals("minicpm-assistente-it:latest", StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault(m => m.Contains("minicpm", StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault();
        RewriteModelBox.ItemsSource = models;
        RewriteModelBox.SelectedItem = preferred;
        TextModelBox.ItemsSource = models;
        TextModelBox.SelectedItem = models.FirstOrDefault(m => m.Equals(_settings.TextModel, StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault(m => m.Equals("qwen3.5:4b", StringComparison.OrdinalIgnoreCase))
            ?? preferred;
        if (TextModelBox.SelectedItem is string textModel) _settings.TextModel = textModel;
        if (preferred == null) RewriteModelBox.Text = _settings.RewriteModel;
    }
    private void SetSetupBusy(bool busy)
    {
        DownloadButton.IsEnabled = ImportButton.IsEnabled = LoadButton.IsEnabled = !busy;
        SettingsPanel.IsEnabled = !busy;
        CancelDownloadButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ModelProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
    private async void DownloadModels(object sender, RoutedEventArgs e) => await InstallModelsAsync(false);
    private async void ImportModels(object sender, RoutedEventArgs e) => await InstallModelsAsync(true);
    private async Task InstallModelsAsync(bool import)
    {
        if (_setup != null || _busy || _recording) return;
        using var setup = new CancellationTokenSource(); _setup = setup;
        _ready = false;
        _wake.Stop(); _wakeListening = false; _badge.SetListening(false);
        _wakeWhisper.Stop();
        _whisper.Stop();
        SetSetupBusy(true);
        try
        {
            var progress = new Progress<(string, double)>(p => { ProgressLabel.Text = p.Item1; ModelProgress.Value = p.Item2; });
            foreach (var model in new[] { Models.Whisper, Models.FastWhisper })
            {
                if (await Storage.VerifyAsync(model, Storage.ModelPath(model), setup.Token)) continue;
                string? source = null;
                if (import)
                {
                    var picker = new Microsoft.Win32.OpenFileDialog { Title = "Seleziona " + model.FileName, FileName = model.FileName, Filter = "Modelli locali|*.bin;*.gguf|Tutti i file|*.*" };
                    if (picker.ShowDialog(this) != true) throw new OperationCanceledException();
                    source = picker.FileName;
                }
                Status(import ? "Importazione del modello" : "Download del modello", model.FileName);
                await Storage.InstallAsync(model, source, progress, setup.Token);
            }
            ModelsLabel.Text = "Whisper locale pronto · verifica MiniCPM in Ollama.";
            ProgressLabel.Text = "Whisper è locale; la riscrittura usa Ollama sul PC.";
            _dictationModel = Models.Whisper; _wakeModel = Models.FastWhisper;
            await WarmupAsync(setup.Token);
        }
        catch (OperationCanceledException) { Status("Operazione annullata", "I modelli già completati restano disponibili."); }
        catch (InvalidDataException) { Status("Modello non valido", "Il file non supera la verifica SHA-256. Importa o scarica il modello esatto indicato."); }
        catch (HttpRequestException) { Status("Download Whisper non riuscito", "Puoi scaricare ggml-small.bin nel browser e poi usare ‘Importa Whisper da disco’. Ollama MiniCPM resta locale."); }
        catch { Status("Preparazione non riuscita", "Controlla connessione, spazio su disco e file dei motori. Puoi riprovare."); }
        finally { _setup = null; SetSetupBusy(false); StartWakeListener(); }
    }
    private void CancelDownload(object sender, RoutedEventArgs e) => _setup?.Cancel();
    private async Task ToggleAsync()
    {
        try
        {
            if (_meetingHeld) { var meeting = _meetings.FirstOrDefault(m => m.Active); if (meeting != null) await meeting.StopAsync(); return; }
            if (_recording) { await StopRecordingAsync(); return; }
            if (_busy || _setup != null) return;
            if (_pending.Length > 0)
            {
                if (_manualOnly && !_recoveryArmed) { _badge.SetStatus("Apri impostazioni per recuperare il testo", "error"); return; }
                await InsertPendingAsync(); return;
            }
            if (!_ready || _desktop == null) { Status("Motori non pronti", "Apri VoceLocale e carica i modelli."); ShowSettings(); return; }
            _busy = true;
            _session = new CancellationTokenSource();
            _wake.Stop(); _wakeListening = false;
            _badge.SetListening(false);
            WakeStatusLabel.Text = "Avvio manuale · rilevamento del comando sospeso durante la dettatura";
            _desktop.BeginWatch();
            _target = await Task.Run(DesktopInput.Capture).WaitAsync(TimeSpan.FromSeconds(2), _session.Token);
            if (_target == null) { CancelSession(); Status("Seleziona un campo scrivibile", "Campi protetti, non riconosciuti o app amministrative non sono utilizzabili.", error: true); return; }
            _desktop.TrackTarget(_target);
            int device = MicrophoneBox.SelectedIndex;
            if (device < 0) throw new IOException();
            BeginSegmentPipeline(_session.Token);
            _recorder.Start(device);
            _recording = true; _busy = false;
            _badge.SetSessionActive(true);
            SettingsPanel.IsEnabled = ModelPanel.IsEnabled = false;
            _recordTime.Restart(); _clock.Start();
            Status("Registrazione · clicca il badge per fermare", "Il microfono è attivo. Esc annulla senza salvare.");
        }
        catch (OperationCanceledException) { }
        catch { CancelSession(); Status("Registrazione non avviata", "Controlla il microfono e il permesso di accesso nelle impostazioni di Windows."); }
    }
    private async Task StopRecordingAsync()
    {
        if (!_recording || _session == null) return;
        _recording = false; _busy = true; _clock.Stop(); _recordTime.Stop();
        _badge.SetSessionActive(false);
        _badge.SetStatus("Trascrizione locale…", "processing");
        var session = _session;
        var sessionToken = session.Token;
        byte[]? audio = null;
        string raw = "";
        try
        {
            audio = await _recorder.StopAsync();
            _segmentChannel?.Writer.TryComplete();
            sessionToken.ThrowIfCancellationRequested();
            if (!TextPolicy.HasVoice(audio)) { FinishSession(); Status("Nessuna voce rilevata", "Avvicinati al microfono e riprova.", error: true); return; }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            if (_segmentWorker != null)
            {
                await _segmentWorker.WaitAsync(timeout.Token);
            }
            if (_segmentsEmitted == 0)
            {
                _badge.SetStatus("Trascrizione locale…", "processing");
                var output = await ProcessBlockAsync(audio, true, timeout.Token);
                if (output.Final == null)
                {
                    Recover(output.Raw, "La correzione locale non è riuscita. Verifica la trascrizione recuperata.", true);
                    return;
                }
                raw = output.Raw;
                if (!string.IsNullOrWhiteSpace(output.Final)) await InsertSegmentAsync(output.Final, timeout.Token);
            }
            Array.Clear(audio); audio = null;
            if (_insertedBlocks == 0) { FinishSession(); Status("Nessuna dettatura riconosciuta", "Dì Eugenio e poi il testo da inserire.", error: true); return; }
            FinishSession(); Status("Testo inserito", $"Inseriti {_insertedBlocks} blocchi puliti. Nessun messaggio è stato inviato.");
        }
        catch (OperationCanceledException)
        {
            if (!sessionToken.IsCancellationRequested && (_segmentFailureText.Length > 0 || raw.Length > 0)) Recover(_segmentFailureText.Length > 0 ? _segmentFailureText : raw, "Elaborazione interrotta. Verifica il blocco recuperato; i blocchi precedenti potrebbero essere già stati inseriti.", _insertedBlocks > 0);
            else if (ReferenceEquals(_session, session)) CancelSession();
        }
        catch
        {
                if (ReferenceEquals(_session, session))
                {
                if (_segmentFailureText.Length > 0 || raw.Length > 0) Recover(_segmentFailureText.Length > 0 ? _segmentFailureText : raw, $"Elaborazione interrotta. Verifica il blocco; {_insertedBlocks} blocchi precedenti, se presenti, sono già stati inseriti.", _insertedBlocks > 0);
                else { FinishSession(); Status("Trascrizione non riuscita", _insertedBlocks > 0 ? $"Elaborazione interrotta dopo {_insertedBlocks} blocchi già inseriti. Controlla il campo prima di riprovare." : "Nessun testo inserito. Controlla microfono e modelli, poi riprova."); }
            }
        }
        finally { if (audio != null) Array.Clear(audio); raw = ""; }
    }

    private void BeginSegmentPipeline(CancellationToken token)
    {
        _lastSpeechLanguage = null;
        _segmentsEmitted = _insertedBlocks = 0;
        _segmentFailureText = "";
        _segmentFailureReason = "";
        _segmentChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        _segmentWorker = ProcessSegmentQueueAsync(_segmentChannel.Reader, token);
        _ = _segmentWorker.ContinueWith(task => _ = task.Exception,
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void OnSegmentReady(byte[] segment)
    {
        Interlocked.Increment(ref _segmentsEmitted);
        var channel = Volatile.Read(ref _segmentChannel);
        if (channel == null || !channel.Writer.TryWrite(segment)) Array.Clear(segment);
    }

    private async Task ProcessSegmentQueueAsync(ChannelReader<byte[]> reader, CancellationToken token)
    {
        var session = _session;
        var channel = _segmentChannel;
        try
        {
            await SpeechBlockQueue.RunPreparedAsync(reader, TimeSpan.FromMilliseconds(1500), TranscribeBlockAsync, ProcessSegmentAsync, token, () => _recorder.HasPendingSpeech);
        }
        catch
        {
            channel?.Writer.TryComplete();
            if (!token.IsCancellationRequested)
                _ = Dispatcher.BeginInvoke(new Action(async () =>
                {
                    if (ReferenceEquals(_session, session)) await StopAfterSegmentErrorAsync();
                }));
            throw;
        }
    }

    private async Task StopAfterSegmentErrorAsync()
    {
        if (!_recording) return;
        _recording = false; _clock.Stop(); _recordTime.Stop(); _badge.SetSessionActive(false);
        try
        {
            var remainingAudio = await _recorder.StopAsync();
            Array.Clear(remainingAudio);
        }
        catch { _recorder.Dispose(); }
        if (_segmentFailureText.Length > 0)
            Recover(_segmentFailureText, $"{_segmentFailureReason} Questo blocco è trattenuto; {_insertedBlocks} blocchi precedenti sono già stati inseriti.", true);
        else
        {
            FinishSession();
            Status("Dettatura interrotta", "Un blocco non è stato elaborato. Controlla il campo prima di riprovare.", error: true);
        }
    }

    private async Task ProcessSegmentAsync(byte[] audio, EngineResponse transcription, CancellationToken token)
    {
        await Dispatcher.InvokeAsync(() => _badge.SetStatus("Elaborazione della frase…", "processing"));
        var output = await FinishBlockAsync(transcription, _insertedBlocks == 0, token);
        if (output.Final == null)
        {
            _segmentFailureText = output.Raw;
            _segmentFailureReason = _settings.TranslateToEnglish ? "Traduzione inglese non validata. La trascrizione originale è conservata." : "MiniCPM non ha prodotto una correzione valida. Verifica la trascrizione originale prima di inserirla.";
            throw new InvalidDataException("Correzione non riuscita.");
        }
        if (!string.IsNullOrWhiteSpace(output.Final)) await InsertSegmentAsync(output.Final, token);
        await Dispatcher.InvokeAsync(() =>
        {
            TimingLabel.Text = $"Ultima frase: trascrizione {output.TranscriptionMs / 1000d:F1} s · correzione {output.RewriteMs / 1000d:F1} s · {_insertedBlocks} blocchi";
            if (_recording) _badge.SetStatus($"Registrazione {_recordTime.Elapsed:mm\\:ss} / 02:00 · clicca per fermare", "recording");
        });
    }

    private async Task InsertSegmentAsync(string text, CancellationToken token)
    {
        if (_desktop == null || _desktop.Changed || _target == null)
        {
            _segmentFailureText = text;
            _segmentFailureReason = "La finestra, il campo o il cursore di destinazione è cambiato.";
            throw new InvalidOperationException("La destinazione è cambiata.");
        }
        string toInsert = _insertedBlocks == 0 ? text : " " + text;
        if (!await _desktop.InsertAsync(_target, toInsert, token))
        {
            _segmentFailureText = text;
            _segmentFailureReason = "Inserimento non completato. Controlla il campo per eventuale testo parziale prima di recuperare il blocco.";
            throw new IOException("Inserimento incompleto.");
        }
        _insertedBlocks++;
    }

    private async Task<(string Raw, string? Final, long TranscriptionMs, long RewriteMs)> ProcessBlockAsync(byte[] audio, bool stripWake, CancellationToken token)
    {
        var transcription = await TranscribeBlockAsync(audio, token);
        return await FinishBlockAsync(transcription, stripWake, token);
    }
    private Task<EngineResponse> TranscribeBlockAsync(byte[] audio, CancellationToken token) =>
        _whisper.RunAsync(new(Guid.NewGuid().ToString(), "transcribe",
            Storage.ModelPath(SpeechProfile.Choose(_settings.TranscriptionProfile, audio.Length, _baseAvailable, _smallAvailable, _settings.Language == "auto" ? _lastSpeechLanguage : _settings.Language)), _settings.Language, audio, Mode: SpeechProfile.DecoderMode(_settings.TranscriptionProfile), Vocabulary: _settings.Vocabulary), token);
    private async Task<(string Raw, string? Final, long TranscriptionMs, long RewriteMs)> FinishBlockAsync(EngineResponse transcription, bool stripWake, CancellationToken token)
    {
        if (!transcription.Ok || string.IsNullOrWhiteSpace(transcription.Text)) throw new IOException("Trascrizione non riuscita.");
        _lastSpeechLanguage = transcription.Language;
        string raw = stripWake ? StripWakePhrase(transcription.Text) : transcription.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return ("", "", transcription.Milliseconds, 0);
        if (_settings.TranslateToEnglish)
        {
            var translated = await _translation.TranslateAsync(_settings.TranslationModel, raw, _settings.Vocabulary, token, transcription.Language);
            return (raw, translated.Ok ? translated.Text : null, transcription.Milliseconds, translated.Milliseconds);
        }
        var correction = await _rewrite.RewriteAsync(_settings.RewriteModel, raw, _settings.Mode, _settings.Vocabulary, token);
        if (!correction.Ok || !TextPolicy.TryValidate(raw, correction.Text ?? "", false, out var final))
            return (raw, null, transcription.Milliseconds, correction.Milliseconds);
        return (raw, final, transcription.Milliseconds, correction.Milliseconds);
    }
    private void Recover(string text, string reason, bool manualOnly)
    {
        _pending = text; _manualOnly = manualOnly; _recoveryArmed = false;
        _busy = false; _recording = false;
        _badge.SetSessionActive(false);
        _desktop?.EndWatch();
        if (_desktop != null) _desktop.Watching = true; // Esc also discards a pending recovery.
        RecoveryText.Text = text; RecoveryHint.Text = reason; RecoveryPanel.Visibility = Visibility.Visible;
        ArmButton.Visibility = manualOnly ? Visibility.Visible : Visibility.Collapsed;
        _badge.SetStatus("Testo pronto · apri impostazioni per recuperarlo", "error");
        Status("Testo trattenuto", reason);
        SettingsPanel.IsEnabled = ModelPanel.IsEnabled = true;
        _tray?.ShowBalloonTip(3000, "VoceLocale", "Il testo è pronto per il recupero nell’app.", Forms.ToolTipIcon.Info);
    }
    private async Task InsertPendingAsync()
    {
        if (_desktop == null) return;
        _busy = true;
        _session?.Dispose(); _session = new CancellationTokenSource();
        var session = _session;
        var token = session.Token;
        _desktop.BeginWatch();
        try
        {
            var target = await Task.Run(DesktopInput.Capture).WaitAsync(TimeSpan.FromSeconds(2), token);
            if (target == null) { Recover(_pending, "Seleziona un campo compatibile e riprova.", _manualOnly); return; }
            _desktop.TrackTarget(target);
            if (await _desktop.InsertAsync(target, _pending, token)) { FinishSession(); Status("Testo inserito", "Pronta per una nuova dettatura."); }
            else Recover(_pending, "Inserimento non completato. Controlla eventuale testo parziale prima di riprovare.", true);
        }
        catch (OperationCanceledException) { }
        catch { if (ReferenceEquals(_session, session) && _pending.Length > 0) Recover(_pending, "Destinazione non disponibile. Controlla eventuale testo parziale.", true); }
        finally { if (ReferenceEquals(_session, session)) _busy = false; }
    }
    private void ArmRecovery(object sender, RoutedEventArgs e)
    {
        _recoveryArmed = true;
        RecoveryHint.Text = "Seleziona ora il campo desiderato e premi la scorciatoia. Verifica prima che non contenga un inserimento parziale.";
    }
    private void CopyRecovery(object sender, RoutedEventArgs e)
    {
        try
        {
            // Explicit user action only. Exclude standard Windows clipboard history and cloud sync.
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, _pending);
            data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)));
            data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)));
            System.Windows.Clipboard.SetDataObject(data, true);
            RecoveryHint.Text = "Copiato negli appunti su tua richiesta. Il testo resterà negli appunti finché non li sostituisci.";
        }
        catch { RecoveryHint.Text = "Appunti occupati. Riprova o seleziona il testo manualmente."; }
    }
    private void DiscardRecovery(object sender, RoutedEventArgs e) => CancelSession();
    private void FinishSession(bool resumeWake = true)
    {
        _segmentChannel?.Writer.TryComplete();
        _clock.Stop(); _desktop?.EndWatch();
        _recording = _busy = _recoveryArmed = _manualOnly = false;
        _badge.SetSessionActive(false);
        _target = null; _pending = ""; RecoveryText.Clear(); RecoveryPanel.Visibility = Visibility.Collapsed;
        _segmentChannel = null; _segmentWorker = null; _segmentFailureText = _segmentFailureReason = "";
        SettingsPanel.IsEnabled = ModelPanel.IsEnabled = true;
        _session?.Dispose(); _session = null;
        if (resumeWake) StartWakeListener();
    }
    private void CancelSession()
    {
        _session?.Cancel(); _segmentChannel?.Writer.TryComplete(); _recorder.Dispose();
        FinishSession(); Status("Dettatura annullata", "Audio e testo temporanei eliminati dall’app.");
    }
    public void HandleUnexpectedError() { CancelSession(); foreach (var meeting in _meetings.Where(m => m.Active)) _ = meeting.StopAsync(); Status("Operazione interrotta", "Nessun nuovo inserimento automatico. Controlla il campo prima di riprovare."); }
    private void HideWindow(object sender, RoutedEventArgs e) => Hide();
    private void ExitApp(object sender, RoutedEventArgs e) => Exit();
    private void OnClosing(object? sender, CancelEventArgs e) { if (!_closing) { e.Cancel = true; Hide(); } }
    private async void Exit()
    {
        if (_closing) return;
        _closing = true; _setup?.Cancel(); CancelSession();
        foreach (var meeting in _meetings) await meeting.ShutdownAsync();
        _whisper.Dispose(); _wakeWhisper.Dispose(); _rewrite.Dispose(); _translation.Dispose(); _desktop?.Dispose();
        _wake.Dispose(); _tray?.Dispose(); _badge.Close();
        System.Windows.Application.Current.Shutdown();
    }
}
