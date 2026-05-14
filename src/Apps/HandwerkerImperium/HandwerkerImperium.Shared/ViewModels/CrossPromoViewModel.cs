using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer die Cross-Promotion-Card. Wird in SettingsView („Mehr"-Tab)
/// und ggf. in WelcomeBackOffer eingeblendet.
/// </summary>
public sealed partial class CrossPromoViewModel : ViewModelBase
{
    private readonly ICrossPromoService _crossPromo;
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    private ObservableCollection<CrossPromoCardItem> _items = [];

    public CrossPromoViewModel(ICrossPromoService crossPromo, ILocalizationService localization)
    {
        _crossPromo = crossPromo;
        _localization = localization;
        Refresh();
    }

    /// <summary>Laedt die Tagesrotation neu (1 Karte als Default).</summary>
    public void Refresh(int count = 1)
    {
        var rotation = _crossPromo.GetCurrentRotation(count);
        var items = new ObservableCollection<CrossPromoCardItem>();
        foreach (var app in rotation)
        {
            items.Add(new CrossPromoCardItem
            {
                App = app,
                Name = _localization.GetString(app.NameKey) ?? app.Id,
                Hook = _localization.GetString(app.HookKey) ?? "",
                IconKind = app.IconKind,
                AccentColor = app.AccentColor,
            });
        }
        Items = items;
    }

    /// <summary>Klick auf eine Karte: Play-Store oeffnen + Analytics-Event feuern.</summary>
    [RelayCommand]
    private void OpenStore(CrossPromoCardItem? item)
    {
        if (item?.App == null) return;
        _crossPromo.TrackClick(item.App);
        try { UriLauncher.OpenUri(item.App.GetPlayStoreUrl()); }
        catch { /* Best-Effort — wenn der Browser fehlt, ignorieren */ }
    }
}

/// <summary>Anzeige-Wrapper fuer eine Cross-Promo-Karte (lokalisierte Strings + App-Referenz).</summary>
public sealed class CrossPromoCardItem
{
    public CrossPromoApp App { get; init; } = null!;
    public string Name { get; init; } = "";
    public string Hook { get; init; } = "";
    public string IconKind { get; init; } = "Apps";
    public string AccentColor { get; init; } = "#7C7FF7";
}
