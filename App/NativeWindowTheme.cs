using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VoceLocale;

internal static class NativeWindowTheme
{
    internal static void FitToWorkArea(Window window)
    {
        double width = Math.Max(320, SystemParameters.WorkArea.Width - 24);
        double height = Math.Max(320, SystemParameters.WorkArea.Height - 24);
        window.MinWidth = Math.Min(window.MinWidth, width); window.MinHeight = Math.Min(window.MinHeight, height);
        window.Width = Math.Min(window.Width, width); window.Height = Math.Min(window.Height, height);
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

    internal static void Apply(Window window)
    {
        // Preferenza della sola finestra: non modifica il tema o le impostazioni di Windows.
        int enabled = 1;
        _ = DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, 20, ref enabled, sizeof(int));
    }
}
