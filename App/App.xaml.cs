using System.Windows;
using System.Windows.Threading;

namespace VoceLocale;

public partial class App : System.Windows.Application
{
    private const string ActivationEventName = @"Local\VoceLocale.Activate.v1";
    private Mutex? _instance;
    private EventWaitHandle? _activationEvent;
    private EventWaitHandle? _meetingActivationEvent;
    private DispatcherTimer? _activationTimer;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool designPreview = e.Args.Contains("--preview-design");
        bool preview = designPreview || e.Args.Contains("--preview");
        // Anteprima grafica indipendente: non chiude documenti di altre sessioni di prova.
        _instance = new Mutex(true, designPreview ? @"Local\Eugenio.DesignPreview.v1" : preview ? @"Local\Eugenio.Preview.v1" : @"Local\VoceLocale.Personal.v1", out bool first);
        if (!first)
        {
            if (preview) { Shutdown(); return; }
            try
            {
                using var activation = EventWaitHandle.OpenExisting(e.Args.Contains("--meeting") ? @"Local\Eugenio.Notetaker.Activate.v1" : ActivationEventName);
                activation.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                MessageBox.Show("VoceLocale si sta avviando. Riprova tra pochi secondi.", "VoceLocale");
            }
            Shutdown();
            return;
        }
        if (!preview)
        {
            _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
            _meetingActivationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Eugenio.Notetaker.Activate.v1");
        }
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            if (MainWindow is MainWindow window) window.HandleUnexpectedError();
            else
            {
                MessageBox.Show($"Avvio di Eugenio non riuscito: {args.Exception.GetType().Name} (0x{args.Exception.HResult:X8}).", "Eugenio · avvio non riuscito");
                Shutdown(-1);
            }
        };
        var main = new MainWindow(preview);
        if (e.Args.Contains("--meeting")) main.Loaded += (_, _) =>
        {
            var meeting = main.ShowMeeting();
            if (preview && e.Args.Contains("--compact")) { meeting.Width = 880; meeting.Height = 620; }
        };
        MainWindow = main;
        main.Show();
        if (preview) return;
        _activationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _activationTimer.Tick += (_, _) =>
        {
            if (_activationEvent?.WaitOne(0) == true) main.ShowSettings();
            if (_meetingActivationEvent?.WaitOne(0) == true) main.ShowMeeting();
        };
        _activationTimer.Start();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _activationTimer?.Stop();
        _activationEvent?.Dispose();
        _meetingActivationEvent?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
