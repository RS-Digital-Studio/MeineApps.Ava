using Avalonia.Controls;

namespace BingXBot.Views;

/// <summary>
/// Desktop-MainView (Sidebar + Content). DataContext wird vom ViewLocator gesetzt.
/// Kein Code-Behind-State: Connection-Dot, Status-Texte und Farben werden vollstaendig
/// via Compiled Bindings gegen MainViewModel-Properties aktualisiert.
/// </summary>
public partial class MainView : UserControl
{
    public MainView() => InitializeComponent();
}
