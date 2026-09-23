using System.Windows;

namespace VoceLocale;

public partial class App : System.Windows.Application
{
    private Mutex? _instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instance = new Mutex(true, @"Local\VoceLocale.Personal.v1", out bool first);
        if (!first) { MessageBox.Show("VoceLocale è già aperta. Usa l’icona nell’area di notifica.", "VoceLocale"); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            if (MainWindow is MainWindow window) window.HandleUnexpectedError();
        };
        var main = new MainWindow();
        MainWindow = main;
        main.Show();
    }
    protected override void OnExit(ExitEventArgs e) { _instance?.Dispose(); base.OnExit(e); }
}
