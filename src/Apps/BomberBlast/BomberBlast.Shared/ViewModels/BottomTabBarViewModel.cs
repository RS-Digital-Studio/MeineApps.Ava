using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel fuer die BottomTabBar (.1 .
/// Subscribt auf IBottomTabHub, propagiert Tab-Wechsel-Commands an den Hub
/// und liefert pro-Tab Brushes (Active = AccentBrush, Inactive = TextMutedBrush).
/// </summary>
public sealed partial class BottomTabBarViewModel : ObservableObject
{
    private readonly IBottomTabHub _hub;

    public BottomTabBarViewModel(IBottomTabHub hub)
    {
        _hub = hub;
        _hub.ActiveTabChanged += _ => RefreshBrushes();
        RefreshBrushes();
    }

    // 8 Brushes (4 Icons + 4 Texte) — bei Tab-Wechsel via OnPropertyChanged() aktualisiert.
    [ObservableProperty] private IBrush _homeIconBrush = Brushes.Gray;
    [ObservableProperty] private IBrush _homeTextBrush = Brushes.Gray;
    [ObservableProperty] private IBrush _playIconBrush = Brushes.Gray;
    [ObservableProperty] private IBrush _playTextBrush = Brushes.Gray;
    [ObservableProperty] private IBrush _shopIconBrush = Brushes.Gray;
    [ObservableProperty] private IBrush _shopTextBrush = Brushes.Gray;
    [ObservableProperty] private IBrush _profileIconBrush = Brushes.Gray;
    [ObservableProperty] private IBrush _profileTextBrush = Brushes.Gray;

    private void RefreshBrushes()
    {
        // Vereinfachte Implementation: Active = Cyan (-Akzent), Inactive = Grau.
        // In Production-Use sollten die Farben aus DynamicResource kommen — diese sind
        // aber in einem ViewModel-Kontext nicht ohne Avalonia-Resource-Resolver greifbar.
        // Hier inline-Farben als Fallback.
        var activeBrush = new SolidColorBrush(Color.FromRgb(0, 220, 220));
        var inactiveBrush = new SolidColorBrush(Color.FromRgb(140, 140, 140));

        HomeIconBrush = _hub.ActiveTab == BottomTab.Home ? activeBrush : inactiveBrush;
        HomeTextBrush = HomeIconBrush;
        PlayIconBrush = _hub.ActiveTab == BottomTab.Play ? activeBrush : inactiveBrush;
        PlayTextBrush = PlayIconBrush;
        ShopIconBrush = _hub.ActiveTab == BottomTab.Shop ? activeBrush : inactiveBrush;
        ShopTextBrush = ShopIconBrush;
        ProfileIconBrush = _hub.ActiveTab == BottomTab.Profile ? activeBrush : inactiveBrush;
        ProfileTextBrush = ProfileIconBrush;
    }

    [RelayCommand]
    private void SwitchTab(string tabName)
    {
        if (Enum.TryParse<BottomTab>(tabName, ignoreCase: false, out var tab))
        {
            _hub.SetActiveTab(tab);
        }
    }
}
