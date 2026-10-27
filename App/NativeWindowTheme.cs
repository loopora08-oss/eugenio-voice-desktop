using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VoceLocale;

internal static class NativeWindowTheme
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

    internal static void Apply(Window window)
    {
        // Preferenza della sola finestra: non modifica il tema o le impostazioni di Windows.
        int enabled = 1;
        _ = DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, 20, ref enabled, sizeof(int));
    }
}
