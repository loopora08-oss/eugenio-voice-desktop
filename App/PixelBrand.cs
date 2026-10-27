using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VoceLocale;

public static class PixelPalette
{
    public static Color BlueText => Color.FromRgb(105, 190, 255);
    internal static readonly Brush[] Stream = [Frozen(12,45,102), Frozen(17,84,192), Frozen(12,144,255), Frozen(89,209,255)];
    private static Brush Frozen(byte r, byte g, byte b) { var brush = new SolidColorBrush(Color.FromRgb(r,g,b)); brush.Freeze(); return brush; }
}

/// <summary>Sprite originale generato per Eugenio, condiviso da tutte le finestre.</summary>
public sealed class PixelLamp : FrameworkElement
{
    private static readonly Lazy<BitmapSource> Artwork = new(() =>
    {
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri("pack://application:,,,/assets/EugenioLampPixel.png"); bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    });
    public PixelLamp() { IsHitTestVisible = false; Focusable = false; RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor); }
    public static ImageSource CreateImage(int frame = 0) => Artwork.Value;
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context); var image = Artwork.Value;
        double scale = Math.Min(ActualWidth / image.PixelWidth, ActualHeight / image.PixelHeight);
        if (scale <= 0) return;
        double width = image.PixelWidth * scale, height = image.PixelHeight * scale;
        context.DrawImage(image, new Rect((ActualWidth-width)/2, (ActualHeight-height)/2, width, height));
    }
}

/// <summary>Scie a pixel statiche nei margini riservati, estranee al percorso di input.</summary>
public sealed class PixelGenieFrame : FrameworkElement
{
    public static readonly DependencyProperty LampAnchorProperty = DependencyProperty.Register(nameof(LampAnchor), typeof(FrameworkElement), typeof(PixelGenieFrame), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public FrameworkElement? LampAnchor { get => (FrameworkElement?)GetValue(LampAnchorProperty); set => SetValue(LampAnchorProperty, value); }
    public PixelGenieFrame()
    {
        IsHitTestVisible = false; Focusable = false; ClipToBounds = true; RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        Loaded += (_, _) => InvalidateVisual();
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(InvalidateVisual));
    }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context); double width = ActualWidth, height = ActualHeight;
        if (width < 100 || height < 100) return;
        const int pixel = 5;
        void Tile(double x, double y, int tone) => context.DrawRectangle(PixelPalette.Stream[tone], null,
            new Rect(Math.Round(x/pixel)*pixel, Math.Round(y/pixel)*pixel, pixel, pixel));
        void Ribbon(Func<double,Point> curve, int count, bool vertical)
        {
            for (int i=0; i<=count; i++)
            {
                double t=i/(double)count; var point=curve(t);
                for (int band=-1; band<=2; band++)
                {
                    double split=band*pixel+Math.Round(Math.Sin(t*24+band*1.7))*pixel;
                    Tile(point.X+(vertical?split:0), point.Y+(vertical?0:split), band==0?2:band==1&&i%5<2?3:Math.Abs(band)==1?1:0);
                }
                if (i%21==4) Tile(point.X+(vertical?22:8), point.Y+(vertical?8:20), 2);
            }
        }
        // Onde ampie come nel concept, con passaggio riservato fuori dai comandi.
        Ribbon(t=>new Point(10+t*(width-26),24+12*Math.Sin(t*15)+5*Math.Sin(t*33)),(int)(width/pixel),false);
        Ribbon(t=>new Point(width-28+13*Math.Sin(t*18),24+t*(height-54)),(int)(height/pixel),true);
        Ribbon(t=>new Point(width*.22+t*(width*.76),height-28+11*Math.Sin(t*20)),(int)(width*.76/pixel),false);
        Point end=new(208,height-170);
        if (LampAnchor is {IsVisible:true,ActualWidth:>0} anchor)
        {
            try { end=anchor.TranslatePoint(new Point(anchor.ActualWidth*.98,anchor.ActualHeight*.28),this); }
            catch(InvalidOperationException) { }
        }
        Ribbon(t=>new Point(26+17*Math.Sin(t*12),24+t*Math.Max(0,end.Y-130)),Math.Max(16,(int)(end.Y/pixel)),true);
        Ribbon(t=>new Point(26+(end.X-26)*t,end.Y-106+106*t+Math.Sin(t*Math.PI*2)*26),58,false);
    }
}
