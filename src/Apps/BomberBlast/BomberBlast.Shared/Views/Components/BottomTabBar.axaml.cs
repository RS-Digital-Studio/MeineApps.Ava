using Avalonia.Controls;

namespace BomberBlast.Views.Components;

/// <summary>
/// Code-behind fuer BottomTabBar (Sprint 3.1 AAA-Audit #4).
/// Strict-MVVM: keine Logic im Code-Behind, ViewLocator setzt DataContext via DI.
/// </summary>
public partial class BottomTabBar : UserControl
{
    public BottomTabBar()
    {
        InitializeComponent();
    }
}
