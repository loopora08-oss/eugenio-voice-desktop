using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace VoceLocale;

/// <summary>Navigazione e marchio condivisi tra pagina principale e Notetaker.</summary>
public sealed class EugenioSidebar : UserControl
{
    private readonly Dictionary<string, RadioButton> _items = [];
    public event Action<string>? NavigationRequested;
    public PixelLamp Lamp { get; } = new() { Width = 196, Height = 124 };
    public string SelectedPage { get => (string)GetValue(SelectedPageProperty); set => SetValue(SelectedPageProperty,value); }
    public static readonly DependencyProperty SelectedPageProperty = DependencyProperty.Register(nameof(SelectedPage),typeof(string),typeof(EugenioSidebar),new PropertyMetadata("dictation",(target,_)=>((EugenioSidebar)target).UpdateSelection()));
    public EugenioSidebar()
    {
        var grid=new Grid(); grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto}); grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var brand=new StackPanel{Margin=new Thickness(8,8,0,32)};
        brand.Children.Add(new TextBlock{Text="Eugenio ≋",FontFamily=new FontFamily("Consolas"),FontSize=30,FontWeight=FontWeights.Bold});
        brand.Children.Add(new TextBlock{Text="Il tuo spazio vocale",FontSize=12,Foreground=(Brush)FindResource("SecondaryInk"),Margin=new Thickness(0,6,0,0)}); grid.Children.Add(brand);
        var navigation=new StackPanel();Grid.SetRow(navigation,1);grid.Children.Add(navigation);
        (string Key,string Label,string Icon)[] destinations=[("dictation","Dettatura","mic"),("meeting","Notetaker","note"),("dictionary","Dizionario","book"),("style","Stile","pen"),("settings","Impostazioni","gear")];
        foreach(var(key,label,icon) in destinations)
        {
            var item=new RadioButton{Style=(Style)FindResource("NavItem"),GroupName="Navigation"+GetHashCode(),Tag=key,MinHeight=46};
            var row=new StackPanel{Orientation=Orientation.Horizontal};
            row.Children.Add(new PixelNavigationIcon(icon){Width=18,Height=18,Margin=new Thickness(0,0,14,0),VerticalAlignment=VerticalAlignment.Center});
            row.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center});item.Content=row;
            System.Windows.Automation.AutomationProperties.SetName(item,label);
            item.Click+=(_,_)=>{NavigationRequested?.Invoke(key);UpdateSelection();};_items.Add(key,item);navigation.Children.Add(item);
        }
        var footer=new StackPanel{Margin=new Thickness(0,12,0,0)};Grid.SetRow(footer,2);grid.Children.Add(footer);
        footer.Children.Add(Lamp);footer.Children.Add(new TextBlock{Text="IA locale · sul tuo PC",FontSize=12,TextAlignment=TextAlignment.Center,Foreground=(Brush)FindResource("SecondaryInk"),Margin=new Thickness(0,14,0,0)});
        Content=grid;UpdateSelection();
    }
    private void UpdateSelection(){foreach(var(key,item) in _items)item.IsChecked=key==SelectedPage;}
}

internal sealed class PixelNavigationIcon(string kind):FrameworkElement
{
    private static readonly Dictionary<string,byte[]> Patterns=new()
    {
        ["mic"]=[0x18,0x3C,0x3C,0x3C,0x5A,0x66,0x18,0x3C],
        ["note"]=[0x7C,0x46,0x5E,0x42,0x5A,0x42,0x7E,0],
        ["book"]=[0x66,0x99,0x99,0x99,0x99,0x99,0x7E,0x18],
        ["pen"]=[0x06,0x0F,0x1E,0x3C,0x78,0x70,0x60,0],
        ["gear"]=[0x18,0x7E,0x66,0xC3,0xC3,0x66,0x7E,0x18]
    };
    protected override void OnRender(DrawingContext context)
    {
        var brush=(Brush)GetValue(TextElement.ForegroundProperty);var rows=Patterns[kind];double step=Math.Min(ActualWidth,ActualHeight)/8;
        for(int y=0;y<8;y++)for(int x=0;x<8;x++)if((rows[y]&(1<<(7-x)))!=0)context.DrawRectangle(brush,null,new Rect(x*step,y*step,step,step));
    }
}
