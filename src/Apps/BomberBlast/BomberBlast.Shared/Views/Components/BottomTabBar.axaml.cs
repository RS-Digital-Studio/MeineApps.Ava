using Avalonia.Controls;

namespace BomberBlast.Views.Components;

/// <summary>
/// Code-behind fuer BottomTabBar (.1 .
/// Strict-MVVM: keine Logic im Code-Behind, ViewLocator setzt DataContext via DI.
/// </summary>
public partial class BottomTabBar : UserControl
{
    public BottomTabBar()
    {
        InitializeComponent();
    }
}
