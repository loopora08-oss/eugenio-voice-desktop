using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VoceLocale;

/// <summary>Always-on-top, draggable microphone badge with a live voice-activity meter.</summary>
public sealed class BadgeWindow : Window
{
    private readonly TextBlock _state;
    private readonly Image _mascot;
    private readonly BitmapSource[] _mascotFrames;
    private readonly TranslateTransform _mascotFloat = new();
    private readonly Border _dot;
    private readonly Border _dotHalo;
    private readonly Border[] _bars;
    private readonly DispatcherTimer _animation = new() { Interval = TimeSpan.FromMilliseconds(90) };
    private readonly Func<Task> _stop;
    private bool _sessionActive;
    private bool _listening;
    private bool _voiceDetected;
    private string _mode = "ready";
    private int _animationStep;
    private int _mascotFrame;
    private int _mascotFrameTicks;
    private int _wakeTransitionTicks;
    public event Action<double, double>? PositionChanged;

    public BadgeWindow(Action openSettings, Func<Task> stop)
    {
        _stop = stop;
        Width = 390; Height = 92; ResizeMode = ResizeMode.NoResize; WindowStyle = WindowStyle.None;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; AllowsTransparency = true;
        Background = Brushes.Transparent; Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Bottom - Height - 24;

        var spriteSheet = BitmapFrame.Create(
            new Uri("pack://application:,,,/assets/EugenioMascot.png"),
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var frameWidth = spriteSheet.PixelWidth / 4;
        _mascotFrames = Enumerable.Range(0, 4)
            .Select(frame => (BitmapSource)new CroppedBitmap(spriteSheet,
                new Int32Rect(frame * frameWidth, 0, frameWidth, spriteSheet.PixelHeight)))
            .ToArray();
        _mascot = new Image
        {
            Source = _mascotFrames[0], Width = 80, Height = 80,
            Stretch = Stretch.Uniform, SnapsToDevicePixels = true,
            RenderTransformOrigin = new Point(.5, .5), RenderTransform = _mascotFloat,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(-5, 0, 7, 0)
        };
        RenderOptions.SetBitmapScalingMode(_mascot, BitmapScalingMode.NearestNeighbor);

        _dotHalo = new Border { Width = 17, Height = 17, CornerRadius = new CornerRadius(9),
            Background = Brush("#3476D7A0"), HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center, Opacity = 0 };
        _dot = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5),
            Background = Brush("#76D7A0"), HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center };
        var indicator = new Grid { Width = 20, Height = 22, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0) };
        indicator.Children.Add(_dotHalo); indicator.Children.Add(_dot);

        _bars = Enumerable.Range(0, 5).Select(_ => new Border
        {
            Width = 3, Height = 4, CornerRadius = new CornerRadius(2),
            Background = Brush("#69D7C4"), VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(1, 1),
            Margin = new Thickness(1, 0, 1, 0), Opacity = .45
        }).ToArray();
        var meter = new StackPanel { Orientation = Orientation.Horizontal, Width = 29,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0) };
        foreach (var bar in _bars) meter.Children.Add(bar);

        var title = new TextBlock { Text = "Eugenio", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Brush("#F2F5F7") };
        _state = new TextBlock { Text = "Avvio…", FontSize = 11, Foreground = Brush("#A9B6BF"), Margin = new Thickness(0, 2, 0, 0) };
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(title); labels.Children.Add(_state);
        var gear = new Button { Content = "⚙", FontSize = 16, Width = 35, Height = 35, Padding = new Thickness(0), Margin = new Thickness(6, 0, 0, 0), ToolTip = "Impostazioni" };
        gear.Click += (_, _) => openSettings();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(_mascot);
        Grid.SetColumn(indicator, 1); grid.Children.Add(indicator);
        Grid.SetColumn(meter, 2); grid.Children.Add(meter);
        Grid.SetColumn(labels, 3); grid.Children.Add(labels);
        Grid.SetColumn(gear, 4); grid.Children.Add(gear);
        var surface = new Border { Background = Brush("#172229"), BorderBrush = Brush("#34444D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(12, 4, 10, 4), Child = grid };
        surface.PreviewMouseLeftButtonDown += async (_, e) =>
        {
            if (e.OriginalSource is DependencyObject source && FindButton(source)) return;
            if (_sessionActive) { e.Handled = true; await _stop(); return; }
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                try { DragMove(); PositionChanged?.Invoke(Left, Top); } catch { }
            }
        };
        Content = surface;
        _animation.Tick += (_, _) => Animate();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(hwnd, -20, GetWindowLongPtr(hwnd, -20) | 0x08000000 | 0x80);
        };
        Closed += (_, _) => _animation.Stop();
    }

    public void RestorePosition(double? left, double? top)
    {
        if (left is null || top is null) return;
        double minX = SystemParameters.VirtualScreenLeft;
        double minY = SystemParameters.VirtualScreenTop;
        double maxX = minX + SystemParameters.VirtualScreenWidth - Width;
        double maxY = minY + SystemParameters.VirtualScreenHeight - Height;
        if (left.Value < minX - Width || left.Value > maxX + Width || top.Value < minY - Height || top.Value > maxY + Height) return;
        Left = Math.Clamp(left.Value, minX, Math.Max(minX, maxX));
        Top = Math.Clamp(top.Value, minY, Math.Max(minY, maxY));
    }

    public void SetStatus(string text, string state = "ready")
    {
        bool wasActive = _mode is "recording" or "processing";
        bool isActive = state is "recording" or "processing";
        _state.Text = text;
        _mode = state;
        _voiceDetected = false;
        if (isActive && !wasActive)
        {
            SetMascotFrame(1);
            _wakeTransitionTicks = 3;
            _mascotFrameTicks = 0;
        }
        else if (!isActive)
        {
            _wakeTransitionTicks = 0;
            _mascotFrameTicks = 0;
            SetMascotFrame(0);
        }
        _dot.Background = Brush(state switch { "recording" => "#F16C63", "processing" => "#F4C96B", "error" => "#F16C63", _ => _listening ? "#76D7A0" : "#87949C" });
        _dotHalo.Background = Brush(state == "recording" ? "#55F16C63" : "#3476D7A0");
        _dotHalo.Opacity = _listening && state == "ready" ? .25 : 0;
        var meterColor = state switch { "recording" => "#FF837B", "processing" => "#F4C96B", _ => "#69D7C4" };
        foreach (var bar in _bars) bar.Background = Brush(meterColor);
        _animationStep = 0;
        _animation.Start();
    }

    /// <summary>Called by the microphone pipeline. Green idle means wake-word standby; bars move only on detected speech.</summary>
    public void SetListening(bool listening, bool voiceDetected = false)
    {
        _listening = listening;
        _voiceDetected = listening && voiceDetected;
        if (_mode == "ready")
        {
            _state.Text = !_listening ? "Microfono non in ascolto" : _voiceDetected ? "Ti sento · dì «Eugenio»" : "Pronto · dì «Eugenio»";
            _dot.Background = Brush(_listening ? (_voiceDetected ? "#69D7C4" : "#76D7A0") : "#87949C");
            _dotHalo.Opacity = _listening ? .25 : 0;
            _animation.Start();
        }
    }

    public void SetVoiceDetected(bool detected)
    {
        _voiceDetected = detected;
        if (_mode == "ready" && _listening)
        {
            _state.Text = detected ? "Ti sento · dì «Eugenio»" : "Pronto · dì «Eugenio»";
            _dot.Background = Brush(detected ? "#69D7C4" : "#76D7A0");
        }
    }

    private void Animate()
    {
        _animationStep++;
        bool active = _mode is "recording" or "processing" || _voiceDetected;
        if (_mode is "recording" or "processing")
        {
            if (_wakeTransitionTicks > 0)
            {
                _wakeTransitionTicks--;
                if (_wakeTransitionTicks == 0) SetMascotFrame(2);
            }
            else if (++_mascotFrameTicks >= 5)
            {
                _mascotFrameTicks = 0;
                SetMascotFrame(_mascotFrame == 2 ? 3 : 2);
            }
            _mascotFloat.Y = -1.4 + Math.Sin(_animationStep * .13) * 2.2;
        }
        else
        {
            _mascotFloat.Y = Math.Sin(_animationStep * .12) * 1.4;
        }
        for (int i = 0; i < _bars.Length; i++)
        {
            double wave = active ? .65 + Math.Abs(Math.Sin((_animationStep * .72) + i * 1.12)) * 2.6 : .85 + Math.Abs(Math.Sin((_animationStep * .16) + i * .55)) * .35;
            ((ScaleTransform)_bars[i].RenderTransform).ScaleY = wave;
            _bars[i].Opacity = active ? .95 : _listening ? .35 : .15;
        }
        _dotHalo.Opacity = _mode == "recording" ? .2 + Math.Abs(Math.Sin(_animationStep * .22)) * .42
            : _listening && _mode == "ready" ? .12 + Math.Abs(Math.Sin(_animationStep * .12)) * .28 : 0;
        var scale = _dotHalo.RenderTransform as ScaleTransform;
        if (scale == null) _dotHalo.RenderTransform = scale = new ScaleTransform(1, 1);
        scale.ScaleX = scale.ScaleY = _mode == "recording" ? .9 + Math.Abs(Math.Sin(_animationStep * .22)) * .55 : 1;
    }

    private void SetMascotFrame(int frame)
    {
        if (_mascotFrame == frame && ReferenceEquals(_mascot.Source, _mascotFrames[frame])) return;
        _mascotFrame = frame;
        _mascot.Source = _mascotFrames[frame];
    }

    public void SetSessionActive(bool active) => _sessionActive = active;
    private static SolidColorBrush Brush(string value) => new((Color)ColorConverter.ConvertFromString(value));
    private static bool FindButton(DependencyObject source)
    {
        for (var parent = source; parent != null; parent = System.Windows.Media.VisualTreeHelper.GetParent(parent))
            if (parent is Button) return true;
        return false;
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
