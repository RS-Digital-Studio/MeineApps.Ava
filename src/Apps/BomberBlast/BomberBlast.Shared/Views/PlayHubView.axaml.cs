using Avalonia.Controls;

namespace BomberBlast.Views;

/// <summary>
/// Code-behind fuer den Play-Hub (Sprint 3.1 AAA-Audit #4).
/// Strict-MVVM: keine Logic im Code-Behind, ViewLocator setzt DataContext via DI.
/// </summary>
public partial class PlayHubView : UserControl
{
    public PlayHubView()
    {
        InitializeComponent();
    }
}
