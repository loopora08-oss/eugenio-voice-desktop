using System.Windows;
using System.Windows.Controls;

namespace VoceLocale;

public partial class MainWindow
{
    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton item) return;
        string section = item.Tag?.ToString() ?? "dictation";
        DictationPage.Visibility = section == "dictation" ? Visibility.Visible : Visibility.Collapsed;
        DictionaryPage.Visibility = section == "dictionary" ? Visibility.Visible : Visibility.Collapsed;
        StylePage.Visibility = section == "style" ? Visibility.Visible : Visibility.Collapsed;
        PreferencesPage.Visibility = section == "settings" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = section switch { "dictionary" => "Dizionario", "style" => "Stile", "settings" => "Impostazioni", _ => "Dettatura" };
    }
}
