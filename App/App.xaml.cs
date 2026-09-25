using System.Windows;
using System.Windows.Threading;

namespace VoceLocale;

public partial class App : System.Windows.Application
{
    private const string ActivationEventName = @"Local\VoceLocale.Activate.v1";
    private Mutex? _instance;
    private EventWaitHandle? _activationEvent;
    private DispatcherTimer? _activationTimer;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instance = new Mutex(true, @"Local\VoceLocale.Personal.v1", out bool first);
        if (!first)
        {
            try
            {
                using var activation = EventWaitHandle.OpenExisting(ActivationEventName);
                activation.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                MessageBox.Show("VoceLocale si sta avviando. Riprova tra pochi secondi.", "VoceLocale");
            }
            Shutdown();
            return;
        }
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            if (MainWindow is MainWindow window) window.HandleUnexpectedError();
        };
        var main = new MainWindow();
        MainWindow = main;
        main.Show();
        _activationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _activationTimer.Tick += (_, _) =>
        {
            if (_activationEvent?.WaitOne(0) == true) main.ShowSettings();
        };
        _activationTimer.Start();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _activationTimer?.Stop();
        _activationEvent?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
