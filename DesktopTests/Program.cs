using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using VoceLocale;
using VoceLocale.Core;

internal static class Program
{
    [DllImport("user32.dll")] private static extern void NotifyWinEvent(uint evt, nint hwnd, int obj, int child);
    [STAThread]
    public static void Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        if (args.Contains("--target")) Target(app);
        else app.Startup += async (_, _) => { try { await Run(app); } catch (Exception e) { Console.WriteLine("FAIL " + e.GetType().Name + ": " + e.Message); Environment.ExitCode = 1; app.Shutdown(); } };
        app.Run();
    }
    private static void Target(Application app)
    {
        var normal = new TextBox { Name = "NormalField", Margin = new Thickness(12), Height = 60, AcceptsReturn = true };
        var other = new TextBox { Name = "OtherField", Margin = new Thickness(12), Height = 50 };
        var password = new PasswordBox { Margin = new Thickness(12) };
        var readOnly = new TextBox { Text = "Sola lettura", IsReadOnly = true, Margin = new Thickness(12) };
        int enters = 0;
        normal.PreviewKeyDown += (_, e) => { if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Return) enters++; };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "Banco prova locale — solo testo sintetico", Margin = new Thickness(12) });
        panel.Children.Add(normal); panel.Children.Add(other); panel.Children.Add(password); panel.Children.Add(readOnly);
        var window = new Window { Title = "VoceLocale — banco prova", Width = 520, Height = 330, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        window.Loaded += (_, _) => normal.Focus();
        window.Show(); window.Activate();
        _ = Task.Run(async () =>
        {
            while (await Console.In.ReadLineAsync() is { } command)
            {
                if (command == "quit") { await app.Dispatcher.InvokeAsync(app.Shutdown); return; }
                var result = await app.Dispatcher.InvokeAsync(() =>
                {
                    switch (command)
                    {
                        case "normal": window.Activate(); normal.Focus(); break;
                        case "other": other.Focus(); break;
                        case "password": password.Focus(); break;
                        case "readonly": readOnly.Focus(); break;
                        case "focus-noise": NotifyWinEvent(0x8005, new WindowInteropHelper(window).Handle, -4, 0); break;
                    }
                    return JsonSerializer.Serialize(new { text = normal.Text, other = other.Text, enters });
                });
                await Console.Out.WriteLineAsync(result);
            }
            await app.Dispatcher.InvokeAsync(app.Shutdown);
        });
    }
    private static async Task Run(Application app)
    {
        using var fixture = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--target")
        { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, CreateNoWindow = true })!;
        using var source = new HwndSource(new HwndSourceParameters("VoceLocale test controller") { ParentWindow = new IntPtr(-3), Width = 1, Height = 1 });
        using var desktop = new DesktopInput(source.Handle);
        int failures = 0;
        void Check(string name, bool passed) { Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {name}"); if (!passed) failures++; }
        async Task<JsonDocument> Command(string value)
        {
            await fixture.StandardInput.WriteLineAsync(value); await fixture.StandardInput.FlushAsync();
            return JsonDocument.Parse(await fixture.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) ?? "{}");
        }
        try
        {
            await Task.Delay(1700);
            using var init = await Command("normal");
            await Task.Delay(300);
            var target = await Task.Run(DesktopInput.Capture);
            Check("normal editable target recognized", target != null);
            if (target == null) throw new InvalidOperationException("Test fixture must be foreground at normal desktop integrity.");
            desktop.BeginWatch();
            desktop.TrackTarget(target);
            using var duplicateFocus = await Command("focus-noise");
            await Task.Delay(350);
            Check("same-editor focus notification does not invalidate destination", !desktop.Changed);
            NotifyWinEvent(0x8005, source.Handle, -4, 0);
            await Task.Delay(350);
            Check("background focus notification does not invalidate destination", !desktop.Changed);
            bool inserted = await desktop.InsertAsync(target, "Caffè, perché?\nNon voglio annullare. 😀", CancellationToken.None);
            await Task.Delay(200);
            using var state = await Command("get");
            Console.WriteLine("Synthetic fixture after insertion: " + state.RootElement.GetRawText() + "; inserted=" + inserted + "; changed=" + desktop.Changed);
            Check("unicode accents and emoji inserted", inserted && state.RootElement.GetProperty("text").GetString() == "Caffè, perché? Non voglio annullare. 😀");
            Check("no Enter key injected", state.RootElement.GetProperty("enters").GetInt32() == 0);
            bool nextBlock = await desktop.InsertAsync(target, " Seconda frase.", CancellationToken.None);
            using var joined = await Command("get");
            Check("dictation blocks keep their separating space", nextBlock && joined.RootElement.GetProperty("text").GetString() == "Caffè, perché? Non voglio annullare. 😀 Seconda frase.");
            using var next = await Command("other");
            for (int wait = 0; wait < 60 && !desktop.Changed; wait++) await Task.Delay(50);
            Check("focus change observed", desktop.Changed);
            Check("old field insertion refused", !await desktop.InsertAsync(target, "DO NOT INSERT", CancellationToken.None));
            using var otherState = await Command("get");
            Console.WriteLine("Synthetic fixture after change: " + otherState.RootElement.GetRawText());
            Check("other field unchanged", otherState.RootElement.GetProperty("other").GetString() == "");
            using var pw = await Command("password"); await Task.Delay(250);
            Check("password target refused", await Task.Run(DesktopInput.Capture) == null);
            using var ro = await Command("readonly"); await Task.Delay(250);
            Check("read-only target refused", await Task.Run(DesktopInput.Capture) == null);
            desktop.EndWatch();
            Check("test hotkey registration", desktop.Configure(new Hotkey(3, 0x79))); // Ctrl+Alt+F10
            using var secondSource = new HwndSource(new HwndSourceParameters("second controller") { ParentWindow = new IntPtr(-3) });
            using var second = new DesktopInput(secondSource.Handle);
            Check("hotkey collision detected", !second.Configure(new Hotkey(3, 0x79)));
            Console.WriteLine($"DESKTOP RESULT: {failures} failures");
            Environment.ExitCode = failures == 0 ? 0 : 1;
        }
        finally
        {
            if (!fixture.HasExited) { await fixture.StandardInput.WriteLineAsync("quit"); await fixture.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
            app.Shutdown();
        }
    }
}
