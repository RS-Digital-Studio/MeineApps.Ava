using Avalonia.Controls;
using Avalonia.Input;
using BingXBot.ViewModels;

namespace BingXBot.Views;

/// <summary>
/// Mobile-Shell fuer Android: Top-Bar + Content + Bottom-Navigation + Mehr-Sheet.
/// DataContext wird vom ViewLocator gesetzt (ContentControl.Content = MainViewModel).
/// </summary>
public partial class MainViewMobile : UserControl
{
    public MainViewMobile()
    {
        InitializeComponent();
    }

    /// <summary>Tap auf das Scrim (Overlay-Hintergrund) schliesst das Bottom-Sheet.</summary>
    private void OnScrimTapped(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsMoreDrawerOpen = false;
    }
}
