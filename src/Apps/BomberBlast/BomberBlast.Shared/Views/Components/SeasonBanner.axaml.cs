using Avalonia.Controls;

namespace BomberBlast.Views.Components;

/// <summary>
/// Audit M20: Saisonales Event-Banner extrahiert aus MainMenuView.axaml.
/// Strict-MVVM: keine Logic im Code-Behind. DataContext erbt automatisch vom Parent (MainMenuViewModel).
/// </summary>
public partial class SeasonBanner : UserControl
{
    public SeasonBanner()
    {
        InitializeComponent();
    }
}
