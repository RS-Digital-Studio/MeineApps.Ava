using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models;
using BomberBlast.Models.Cosmetics;

namespace BomberBlast.ViewModels;

/// <summary>
/// Customize-Picker-Partial des ProfileViewModel (v2.0.43, Plan Phase 4).
///
/// Zeigt die sechs Cosmetic-Slots des Spielers als Quick-Switcher direkt im Profile-Hub.
/// Fuer jeden Slot gibt es eine ObservableCollection&lt;CustomizationItem&gt; mit allen
/// verfuegbaren Optionen — Owned-Items sind klickbar (Tap → SelectXxx), Locked-Items
/// sind ausgegraut (Spieler muss in den Shop fuer den Kauf).
///
/// Slots:
/// <list type="bullet">
///   <item>Player-Skin (24 Varianten)</item>
///   <item>Bomb-Skin (14 Varianten)</item>
///   <item>Explosion-Skin (15 Varianten)</item>
///   <item>Trail (~10-15 Varianten)</item>
///   <item>Victory-Animation (~5-10 Varianten)</item>
///   <item>Profilrahmen (~5-10 Varianten)</item>
/// </list>
///
/// Da fuer XAML-DataTemplates keine generischen Items moeglich sind, nutzt jeder Slot
/// eine eigene <see cref="CustomizationItem"/>-Liste mit Slot-Tag fuer den Activate-Command.
/// </summary>
public sealed partial class ProfileViewModel
{
    [ObservableProperty] private string _customizeHintText = "";
    [ObservableProperty] private string _customizeShopHintText = "";
    [ObservableProperty] private string _customizeSectionPlayerSkinTitle = "";
    [ObservableProperty] private string _customizeSectionBombSkinTitle = "";
    [ObservableProperty] private string _customizeSectionExplosionSkinTitle = "";
    [ObservableProperty] private string _customizeSectionTrailTitle = "";
    [ObservableProperty] private string _customizeSectionVictoryTitle = "";
    [ObservableProperty] private string _customizeSectionFrameTitle = "";

    public ObservableCollection<CustomizationItem> PlayerSkinItems { get; } = new();
    public ObservableCollection<CustomizationItem> BombSkinItems { get; } = new();
    public ObservableCollection<CustomizationItem> ExplosionSkinItems { get; } = new();
    public ObservableCollection<CustomizationItem> TrailItems { get; } = new();
    public ObservableCollection<CustomizationItem> VictoryItems { get; } = new();
    public ObservableCollection<CustomizationItem> FrameItems { get; } = new();

    /// <summary>
    /// Lokalisierte Section-Header und Hinweis-Texte aktualisieren.
    /// </summary>
    private void UpdateCustomizationLabels()
    {
        CustomizeHintText = _localization.GetString("ProfileCustomizeHint")
            ?? "Tap any unlocked item to apply it. Locked items can be bought in the Shop.";
        CustomizeShopHintText = _localization.GetString("ProfileCustomizeShopHint") ?? "More in Shop";
        CustomizeSectionPlayerSkinTitle = _localization.GetString("ProfileCustomizePlayerSkin") ?? "Player Skin";
        CustomizeSectionBombSkinTitle = _localization.GetString("ProfileCustomizeBombSkin") ?? "Bomb";
        CustomizeSectionExplosionSkinTitle = _localization.GetString("ProfileCustomizeExplosionSkin") ?? "Explosion";
        CustomizeSectionTrailTitle = _localization.GetString("ProfileCustomizeTrail") ?? "Trail";
        CustomizeSectionVictoryTitle = _localization.GetString("ProfileCustomizeVictory") ?? "Victory";
        CustomizeSectionFrameTitle = _localization.GetString("ProfileCustomizeFrame") ?? "Frame";
    }

    /// <summary>
    /// Fuellt alle 6 Listen aus dem CustomizationService neu. Wird bei OnAppearing
    /// + Tab-Wechsel Customize aufgerufen, sowie nach jedem SelectXxx (damit IsActive-Markierungen
    /// stimmen).
    /// </summary>
    private void RefreshCustomizationItems()
    {
        UpdateCustomizationLabels();

        PlayerSkinItems.Clear();
        foreach (var s in _customizationService.AvailablePlayerSkins)
        {
            string colorHex = $"#{s.PrimaryColor.Red:X2}{s.PrimaryColor.Green:X2}{s.PrimaryColor.Blue:X2}";
            PlayerSkinItems.Add(new CustomizationItem(
                slot: "PlayerSkin",
                id: s.Id,
                displayName: _localization.GetString(s.NameKey) ?? s.Id,
                primaryColor: colorHex,
                isOwned: _customizationService.IsPlayerSkinOwned(s.Id),
                isActive: _customizationService.PlayerSkin.Id == s.Id,
                rarity: s.Rarity));
        }

        BombSkinItems.Clear();
        foreach (var s in _customizationService.AvailableBombSkins)
        {
            string colorHex = s.BodyColor == SkiaSharp.SKColor.Empty
                ? "#FFFFFF"
                : $"#{s.BodyColor.Red:X2}{s.BodyColor.Green:X2}{s.BodyColor.Blue:X2}";
            BombSkinItems.Add(new CustomizationItem(
                slot: "BombSkin",
                id: s.Id,
                displayName: _localization.GetString(s.NameKey) ?? s.Id,
                primaryColor: colorHex,
                isOwned: _customizationService.IsBombSkinOwned(s.Id),
                isActive: _customizationService.BombSkin.Id == s.Id,
                rarity: s.Rarity));
        }

        ExplosionSkinItems.Clear();
        foreach (var s in _customizationService.AvailableExplosionSkins)
        {
            string colorHex = s.OuterColor == SkiaSharp.SKColor.Empty
                ? "#FFFFFF"
                : $"#{s.OuterColor.Red:X2}{s.OuterColor.Green:X2}{s.OuterColor.Blue:X2}";
            ExplosionSkinItems.Add(new CustomizationItem(
                slot: "ExplosionSkin",
                id: s.Id,
                displayName: _localization.GetString(s.NameKey) ?? s.Id,
                primaryColor: colorHex,
                isOwned: _customizationService.IsExplosionSkinOwned(s.Id),
                isActive: _customizationService.ExplosionSkin.Id == s.Id,
                rarity: s.Rarity));
        }

        TrailItems.Clear();
        foreach (var t in _customizationService.AvailableTrails)
        {
            string colorHex = $"#{t.PrimaryColor.Red:X2}{t.PrimaryColor.Green:X2}{t.PrimaryColor.Blue:X2}";
            TrailItems.Add(new CustomizationItem(
                slot: "Trail",
                id: t.Id,
                displayName: _localization.GetString(t.NameKey) ?? t.Id,
                primaryColor: colorHex,
                isOwned: _customizationService.IsTrailOwned(t.Id),
                isActive: (_customizationService.ActiveTrail?.Id ?? "") == t.Id,
                rarity: t.Rarity));
        }

        VictoryItems.Clear();
        foreach (var v in _customizationService.AvailableVictories)
        {
            VictoryItems.Add(new CustomizationItem(
                slot: "Victory",
                id: v.Id,
                displayName: _localization.GetString(v.NameKey) ?? v.Id,
                primaryColor: "#FFD700",
                isOwned: _customizationService.IsVictoryOwned(v.Id),
                isActive: (_customizationService.ActiveVictory?.Id ?? "") == v.Id,
                rarity: v.Rarity));
        }

        FrameItems.Clear();
        foreach (var f in _customizationService.AvailableFrames)
        {
            string colorHex = $"#{f.PrimaryColor.Red:X2}{f.PrimaryColor.Green:X2}{f.PrimaryColor.Blue:X2}";
            FrameItems.Add(new CustomizationItem(
                slot: "Frame",
                id: f.Id,
                displayName: _localization.GetString(f.NameKey) ?? f.Id,
                primaryColor: colorHex,
                isOwned: _customizationService.IsFrameOwned(f.Id),
                isActive: (_customizationService.ActiveFrame?.Id ?? "") == f.Id,
                rarity: f.Rarity));
        }
    }

    /// <summary>
    /// Aktiviert ein Customization-Item per "Slot:Id"-Kombination (XAML CommandParameter).
    /// </summary>
    [RelayCommand]
    private void ActivateCustomization(CustomizationItem? item)
    {
        if (item == null || !item.IsOwned) return;

        switch (item.Slot)
        {
            case "PlayerSkin": _customizationService.SetPlayerSkin(item.Id); break;
            case "BombSkin": _customizationService.SetBombSkin(item.Id); break;
            case "ExplosionSkin": _customizationService.SetExplosionSkin(item.Id); break;
            case "Trail": _customizationService.SetTrail(item.Id); break;
            case "Victory": _customizationService.SetVictory(item.Id); break;
            case "Frame": _customizationService.SetFrame(item.Id); break;
            default: return;
        }

        // UI-Refresh: Items neu aufbauen damit IsActive-Markierungen stimmen,
        // und ProfileView-Header (ActiveSkinName/ActiveSkinColor) aktualisieren.
        RefreshCustomizationItems();
        var skin = _customizationService.PlayerSkin;
        ActiveSkinName = _localization.GetString(skin.NameKey) ?? skin.Id;
        ActiveSkinColor = $"#{skin.PrimaryColor.Red:X2}{skin.PrimaryColor.Green:X2}{skin.PrimaryColor.Blue:X2}";
        var frame = _customizationService.ActiveFrame;
        ActiveFrameName = frame != null
            ? (_localization.GetString(frame.NameKey) ?? frame.Id)
            : (_localization.GetString("ProfileNoFrame") ?? "-");

        // Mini-Feedback ohne Confetti-Spam
        var msg = _localization.GetString("ProfileCustomizeApplied") ?? "Applied!";
        FloatingTextRequested?.Invoke($"{msg} {item.DisplayName}", "success");
    }

    /// <summary>
    /// Navigations-Shortcut zum Shop fuer locked Items.
    /// </summary>
    [RelayCommand]
    private void GoToShop() => NavigationRequested?.Invoke(new GoShop());
}

/// <summary>
/// View-Model-DTO fuer das Customize-ItemsControl. Enthaelt Slot-Tag (fuer Activate-Command),
/// Display-Daten und Status-Flags (Owned/Active/Rarity).
/// </summary>
public sealed class CustomizationItem
{
    public string Slot { get; }
    public string Id { get; }
    public string DisplayName { get; }
    public string PrimaryColor { get; }
    public bool IsOwned { get; }
    public bool IsActive { get; }
    public Rarity Rarity { get; }

    /// <summary>0.4 fuer locked, 1.0 fuer owned. XAML Opacity-Bind.</summary>
    public double CardOpacity => IsOwned ? 1.0 : 0.4;

    /// <summary>Border-Color je nach Status: AccentColor wenn aktiv, RarityColor wenn owned, grau wenn locked.</summary>
    public string BorderHex => IsActive
        ? "#FFD700"
        : IsOwned ? RarityHex(Rarity) : "#404040";

    /// <summary>Lock-Icon sichtbar wenn nicht owned.</summary>
    public bool IsLocked => !IsOwned;

    public CustomizationItem(string slot, string id, string displayName, string primaryColor,
        bool isOwned, bool isActive, Rarity rarity)
    {
        Slot = slot;
        Id = id;
        DisplayName = displayName;
        PrimaryColor = primaryColor;
        IsOwned = isOwned;
        IsActive = isActive;
        Rarity = rarity;
    }

    private static string RarityHex(Rarity r) => r switch
    {
        Rarity.Legendary => "#FFD700",
        Rarity.Epic => "#9C27B0",
        Rarity.Rare => "#2196F3",
        _ => "#FFFFFF"
    };
}
