using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace VoceLocale;

public sealed class StatusOverlay : Window
{
    private readonly TextBlock _label;
    public StatusOverlay()
    {
        Width = 480; Height = 64; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; AllowsTransparency = true;
        Background = Brushes.Transparent; IsHitTestVisible = false;
        _label = new TextBlock { Foreground = Brushes.White, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center };
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.Children.Add(new PixelLamp { Width = 48, Height = 32 });
        Grid.SetColumn(_label, 1); layout.Children.Add(_label);
        Content = new Border { Background = Brushes.Black, BorderBrush = PixelPalette.Stream[1], BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2), Padding = new Thickness(10), Child = layout };
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(hwnd, -20, GetWindowLongPtr(hwnd, -20) | 0x08000000 | 0x80 | 0x20);
        };
    }
    public void Update(string text)
    {
        _label.Text = text;
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Bottom - Height - 24;
        if (!IsVisible) Show();
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
