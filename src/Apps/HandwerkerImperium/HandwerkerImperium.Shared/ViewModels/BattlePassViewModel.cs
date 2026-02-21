using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel für den Battle Pass (30 Tiers, Free + Premium Track, 30-Tage-Saison).
/// </summary>
public partial class BattlePassViewModel : ObservableObject
{
    private readonly IGameStateService _gameStateService;
    private readonly IBattlePassService _battlePassService;
    private readonly ILocalizationService _localizationService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    /// <summary>
    /// Event für Alert-Dialoge. Parameter: Titel, Nachricht, Button-Text.
    /// </summary>
    public event Action<string, string, string>? AlertRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private ObservableCollection<BattlePassTierDisplay> _tiers = [];

    [ObservableProperty]
    private int _currentTier;

    /// <summary>
    /// Aktuelle Tier-Anzeige (z.B. "Tier 5/30").
    /// </summary>
    [ObservableProperty]
    private string _currentTierDisplay = "";

    /// <summary>
    /// Breite des XP-Fortschrittsbalkens (gebunden an Border.Width).
    /// </summary>
    [ObservableProperty]
    private double _xpBarWidth;

    /// <summary>
    /// XP-Fortschrittsanzeige (z.B. "500 / 2000 XP").
    /// </summary>
    [ObservableProperty]
    private string _xpProgressDisplay = "";

    [ObservableProperty]
    private double _xpProgress;

    /// <summary>
    /// Ob der Spieler den Premium-Pass hat.
    /// </summary>
    [ObservableProperty]
    private bool _isPremiumPass;

    /// <summary>
    /// Verbleibende Saison-Zeit.
    /// </summary>
    [ObservableProperty]
    private string _seasonTimeRemainingDisplay = "";

    /// <summary>
    /// Preis-Anzeige für Premium-Upgrade.
    /// </summary>
    [ObservableProperty]
    private string _upgradePriceDisplay = "";

    [ObservableProperty]
    private int _seasonNumber;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public BattlePassViewModel(
        IGameStateService gameStateService,
        IBattlePassService battlePassService,
        ILocalizationService localizationService)
    {
        _gameStateService = gameStateService;
        _battlePassService = battlePassService;
        _localizationService = localizationService;

        UpdateLocalizedTexts();
        RefreshBattlePass();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ClaimTier(BattlePassTierDisplay? tierDisplay)
    {
        if (tierDisplay == null) return;

        _battlePassService.ClaimReward(tierDisplay.TierNumber, tierDisplay.IsPremiumLocked);
        RefreshBattlePass();
    }

    [RelayCommand]
    private void UpgradeToPremium()
    {
        _battlePassService.UpgradeToPremium();
        RefreshBattlePass();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktualisiert alle Battle-Pass-Daten aus dem State.
    /// </summary>
    public void RefreshBattlePass()
    {
        var state = _gameStateService.State;
        var bp = state.BattlePass;

        CurrentTier = bp.CurrentTier;
        CurrentTierDisplay = $"Tier {bp.CurrentTier}/{BattlePass.MaxTier}";
        XpProgress = bp.TierProgress;
        XpProgressDisplay = $"{bp.CurrentXp} / {bp.XpForNextTier} XP";

        // XP-Balkenbreite (geschätzt auf ca. 200px Maximalbreite)
        XpBarWidth = bp.TierProgress * 200.0;

        IsPremiumPass = bp.IsPremium;
        SeasonNumber = bp.SeasonNumber;

        // Premium-Upgrade-Preis
        UpgradePriceDisplay = $"50 GS → {(_localizationService.GetString("PremiumPass") ?? "Premium Pass")}";

        // Verbleibende Zeit
        int daysLeft = bp.DaysRemaining;
        SeasonTimeRemainingDisplay = daysLeft > 1
            ? $"{daysLeft} {(_localizationService.GetString("Days") ?? "Tage")}"
            : daysLeft == 1
                ? $"1 {(_localizationService.GetString("Day") ?? "Tag")}"
                : _localizationService.GetString("Expired") ?? "Abgelaufen";

        // Tier-Displays aufbauen
        BuildTierDisplays(bp);
    }

    /// <summary>
    /// Lokalisierte Texte aktualisieren.
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("BattlePass") ?? "Battle Pass";
        RefreshBattlePass();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildTierDisplays(BattlePass bp)
    {
        var state = _gameStateService.State;
        decimal baseIncome = Math.Max(1m, state.TotalIncomePerSecond);
        var freeRewards = BattlePass.GenerateFreeRewards(baseIncome);
        var premiumRewards = BattlePass.GeneratePremiumRewards(baseIncome);

        var items = new ObservableCollection<BattlePassTierDisplay>();

        for (int i = 0; i < BattlePass.MaxTier; i++)
        {
            var freeReward = freeRewards.Count > i ? freeRewards[i] : null;
            var premiumReward = premiumRewards.Count > i ? premiumRewards[i] : null;

            bool isUnlocked = i < bp.CurrentTier;
            bool isCurrent = i == bp.CurrentTier;
            bool freeRewardClaimed = bp.ClaimedFreeTiers.Contains(i);
            bool premiumRewardClaimed = bp.ClaimedPremiumTiers.Contains(i);

            // Kann beansprucht werden: Tier freigeschaltet und noch nicht beansprucht
            bool canClaim = isUnlocked && (!freeRewardClaimed || (!premiumRewardClaimed && bp.IsPremium));

            // Bereits beansprucht: Free + Premium (wenn Premium aktiv)
            bool isClaimed = freeRewardClaimed && (!bp.IsPremium || premiumRewardClaimed);

            items.Add(new BattlePassTierDisplay
            {
                TierNumber = i,
                FreeRewardName = FormatReward(freeReward),
                FreeRewardIcon = GetRewardIcon(freeReward),
                PremiumRewardName = FormatReward(premiumReward),
                PremiumRewardIcon = GetRewardIcon(premiumReward),
                PremiumRewardColor = "#FFD700",
                IsCurrent = isCurrent,
                IsUnlocked = isUnlocked,
                IsPremiumLocked = !bp.IsPremium,
                CanClaim = canClaim,
                IsClaimed = isClaimed,

                // Visuelle Eigenschaften
                TierBackground = isCurrent ? "#30D97706" : isUnlocked ? "#10FFFFFF" : "#08FFFFFF",
                TierBorderBrush = isCurrent ? "#D97706" : isUnlocked ? "#40FFFFFF" : "#20FFFFFF",
                TierBorderThickness = isCurrent ? 2.0 : 1.0,
                TierNumberBackground = isCurrent ? "#D97706" : isUnlocked ? "#92400E" : "#404040"
            });
        }

        Tiers = items;
    }

    private string FormatReward(BattlePassReward? reward)
    {
        if (reward == null) return "-";

        var parts = new List<string>();
        if (reward.MoneyReward > 0)
            parts.Add(MoneyFormatter.Format(reward.MoneyReward, 0));
        if (reward.XpReward > 0)
            parts.Add($"{reward.XpReward} XP");
        if (reward.GoldenScrewReward > 0)
            parts.Add($"{reward.GoldenScrewReward} GS");

        return parts.Count > 0 ? string.Join(" + ", parts) : "-";
    }

    private static string GetRewardIcon(BattlePassReward? reward)
    {
        if (reward == null) return "";
        if (reward.GoldenScrewReward > 0) return "Cog";
        if (reward.MoneyReward > 0) return "Cash";
        if (reward.XpReward > 0) return "Star";
        return "Gift";
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DISPLAY MODEL
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Anzeige-Modell für ein Battle-Pass-Tier im UI.
/// </summary>
public class BattlePassTierDisplay
{
    public int TierNumber { get; set; }
    public string FreeRewardName { get; set; } = "";
    public string FreeRewardIcon { get; set; } = "";
    public string PremiumRewardName { get; set; } = "";
    public string PremiumRewardIcon { get; set; } = "";
    public string PremiumRewardColor { get; set; } = "#FFD700";
    public bool IsCurrent { get; set; }
    public bool IsUnlocked { get; set; }
    public bool IsPremiumLocked { get; set; }
    public bool CanClaim { get; set; }
    public bool IsClaimed { get; set; }

    // Visuelle Eigenschaften
    public string TierBackground { get; set; } = "#08FFFFFF";
    public string TierBorderBrush { get; set; } = "#20FFFFFF";
    public double TierBorderThickness { get; set; } = 1.0;
    public string TierNumberBackground { get; set; } = "#404040";

    /// <summary>
    /// Opacity: Gesperrte Tiers gedimmt.
    /// </summary>
    public double DisplayOpacity => IsUnlocked || IsCurrent ? 1.0 : 0.5;
}
