using BomberBlast.Models.BattlePass;
using BomberBlast.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für den Battle Pass (30-Tier Saison mit Free/Premium-Track).
/// Zeigt Tier-Liste, XP-Fortschritt und ermöglicht Reward-Claims.
/// </summary>
public partial class BattlePassViewModel : ObservableObject, INavigable, IGameJuiceEmitter
{
    private readonly IBattlePassService _battlePassService;
    private readonly ILocalizationService _localizationService;
    private readonly IGemService _gemService;

    // Gecachte Tier-Definitionen (statisch, ändern sich nicht)
    private readonly BattlePassReward[] _freeRewards;
    private readonly BattlePassReward[] _premiumRewards;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    /// <summary>Premium-Kauf anfordern (wird an MainViewModel delegiert für IAP)</summary>
    public event Action? PremiumPurchaseRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _title = "Battle Pass";

    [ObservableProperty]
    private string _tierLabel = "Tier";

    [ObservableProperty]
    private string _currentTierDisplay = "";

    [ObservableProperty]
    private string _xpProgressDisplay = "";

    [ObservableProperty]
    private double _xpProgress;

    [ObservableProperty]
    private string _seasonTimeDisplay = "";

    [ObservableProperty]
    private string _seasonNumberDisplay = "";

    [ObservableProperty]
    private bool _isPremiumPass;

    [ObservableProperty]
    private string _upgradeButtonText = "";

    [ObservableProperty]
    private List<BattlePassTierDisplayItem> _tiers = [];

    /// <summary>Ob der 2x XP-Boost aktiv ist</summary>
    [ObservableProperty]
    private bool _isXpBoostActive;

    /// <summary>Verbleibende Zeit des XP-Boosts (z.B. "23:45:12")</summary>
    [ObservableProperty]
    private string _xpBoostTimeText = "";

    /// <summary>Lokalisierter Text für den XP-Boost-Button</summary>
    [ObservableProperty]
    private string _xpBoostButtonText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public BattlePassViewModel(IBattlePassService battlePassService, ILocalizationService localizationService, IGemService gemService)
    {
        _battlePassService = battlePassService;
        _localizationService = localizationService;
        _gemService = gemService;

        // Tier-Definitionen einmal laden
        _freeRewards = BattlePassTierDefinitions.GetFreeRewards();
        _premiumRewards = BattlePassTierDefinitions.GetPremiumRewards();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wird beim Anzeigen aufgerufen - aktualisiert alle Daten und Texte.
    /// </summary>
    public void OnAppearing()
    {
        // Neue Saison prüfen/starten
        _battlePassService.CheckAndStartNewSeason();

        RefreshState();
        UpdateLocalizedTexts();
        UpdateXpBoostState();
    }

    /// <summary>
    /// Alle lokalisierten Texte aktualisieren (nach Sprachwechsel).
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("BattlePassTitle") ?? "Battle Pass";
        TierLabel = _localizationService.GetString("BattlePassTierLabel") ?? "Tier";
        UpgradeButtonText = _localizationService.GetString("BattlePassUpgrade") ?? "Premium (2,99 EUR)";

        var data = _battlePassService.Data;

        // Saison-Anzeige
        var seasonFormat = _localizationService.GetString("BattlePassSeason") ?? "Season {0}";
        SeasonNumberDisplay = string.Format(seasonFormat, data.SeasonNumber);

        // Tier-Anzeige
        var tierFormat = _localizationService.GetString("BattlePassTierFormat") ?? "Tier {0}/{1}";
        CurrentTierDisplay = string.Format(tierFormat, data.CurrentTier, BattlePassTierDefinitions.MaxTier);

        // XP-Anzeige
        UpdateXpDisplay(data);

        // Verbleibende Zeit
        UpdateTimeDisplay(data);

        // Tier-Liste Texte aktualisieren
        RefreshTierTexts();
    }

    /// <summary>
    /// Gesamten State aus dem Service neu laden und Tier-Liste aufbauen.
    /// </summary>
    public void RefreshState()
    {
        var data = _battlePassService.Data;

        IsPremiumPass = data.IsPremium;
        XpProgress = data.TierProgress;

        // Tier-Anzeige
        var tierFormat = _localizationService.GetString("BattlePassTierFormat") ?? "Tier {0}/{1}";
        CurrentTierDisplay = string.Format(tierFormat, data.CurrentTier, BattlePassTierDefinitions.MaxTier);

        UpdateXpDisplay(data);
        UpdateTimeDisplay(data);
        BuildTierList(data);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Free-Track Belohnung für ein Tier beanspruchen.
    /// </summary>
    [RelayCommand]
    private void ClaimFreeReward(int tierIndex)
    {
        var reward = _battlePassService.ClaimReward(tierIndex, false);
        if (reward == null) return;

        // Floating-Text mit Belohnungs-Info
        var text = FormatRewardText(reward);
        FloatingTextRequested?.Invoke(text, "+");

        RefreshState();
    }

    /// <summary>
    /// Premium-Track Belohnung für ein Tier beanspruchen.
    /// </summary>
    [RelayCommand]
    private void ClaimPremiumReward(int tierIndex)
    {
        var reward = _battlePassService.ClaimReward(tierIndex, true);
        if (reward == null) return;

        // Floating-Text mit Belohnungs-Info
        var text = FormatRewardText(reward);
        FloatingTextRequested?.Invoke(text, "+");

        // Cosmetic-Rewards bekommen eine Celebration
        if (reward.Type == BattlePassRewardType.Cosmetic)
        {
            CelebrationRequested?.Invoke();
        }

        RefreshState();
    }

    /// <summary>
    /// 2x XP-Boost für 20 Gems aktivieren (24 Stunden Dauer).
    /// </summary>
    [RelayCommand]
    private void ActivateXpBoost()
    {
        if (_battlePassService.IsXpBoostActive)
        {
            var alreadyActive = _localizationService.GetString("XpBoostActive") ?? "Active! {0} remaining";
            FloatingTextRequested?.Invoke(string.Format(alreadyActive, GetBoostRemainingText()), "cyan");
            return;
        }

        if (!_gemService.CanAfford(20))
        {
            var noGems = _localizationService.GetString("ShopNotEnoughGems") ?? "Not enough Gems!";
            FloatingTextRequested?.Invoke(noGems, "red");
            return;
        }

        if (_battlePassService.ActivateXpBoost())
        {
            var boostTitle = _localizationService.GetString("XpBoostTitle") ?? "2x XP Boost";
            FloatingTextRequested?.Invoke($"{boostTitle}!", "cyan");
            CelebrationRequested?.Invoke();
            UpdateXpBoostState();
        }
    }

    /// <summary>
    /// Premium-Kauf anstoßen (delegiert an MainViewModel via Event).
    /// Ruft NICHT direkt ActivatePremium() auf - das passiert nach erfolgreichem IAP.
    /// </summary>
    [RelayCommand]
    private void UpgradeToPremium()
    {
        if (IsPremiumPass) return;
        PremiumPurchaseRequested?.Invoke();
    }

    /// <summary>
    /// Zurück navigieren.
    /// </summary>
    [RelayCommand]
    private void Back()
    {
        NavigationRequested?.Invoke(new GoBack());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EXTERN AUFRUFBAR
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wird von MainViewModel aufgerufen wenn der IAP-Kauf erfolgreich war.
    /// Aktiviert den Premium-Pass und aktualisiert die Anzeige.
    /// </summary>
    public void OnPremiumPurchaseConfirmed()
    {
        _battlePassService.ActivatePremium();
        RefreshState();
        UpdateLocalizedTexts();
        CelebrationRequested?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>XP-Fortschritts-Anzeige aktualisieren</summary>
    private void UpdateXpDisplay(BattlePassData data)
    {
        if (data.CurrentTier >= BattlePassTierDefinitions.MaxTier)
        {
            XpProgressDisplay = _localizationService.GetString("BattlePassMaxTier") ?? "MAX";
            XpProgress = 1.0;
        }
        else
        {
            XpProgressDisplay = $"{data.CurrentXp}/{data.XpForNextTier} XP";
            XpProgress = data.TierProgress;
        }

        // XP-Bar nutzt ScaleTransform mit XpProgress (0.0-1.0) in XAML
    }

    /// <summary>Verbleibende Zeit aktualisieren</summary>
    private void UpdateTimeDisplay(BattlePassData data)
    {
        int days = data.DaysRemaining;
        if (days <= 0)
        {
            SeasonTimeDisplay = _localizationService.GetString("BattlePassExpired") ?? "Expired";
        }
        else if (days == 1)
        {
            var format = _localizationService.GetString("BattlePassDayRemaining") ?? "{0} day left";
            SeasonTimeDisplay = string.Format(format, days);
        }
        else
        {
            var format = _localizationService.GetString("BattlePassDaysRemaining") ?? "{0} days left";
            SeasonTimeDisplay = string.Format(format, days);
        }
    }

    /// <summary>Tier-Liste komplett neu aufbauen</summary>
    private void BuildTierList(BattlePassData data)
    {
        var claimText = _localizationService.GetString("BattlePassClaim") ?? "Claim";
        var items = new List<BattlePassTierDisplayItem>(BattlePassTierDefinitions.MaxTier);

        for (int i = 0; i < BattlePassTierDefinitions.MaxTier; i++)
        {
            bool isUnlocked = i < data.CurrentTier;
            bool isCurrent = i == data.CurrentTier;
            bool isFreeClaimed = data.ClaimedFreeTiers.Contains(i);
            bool isPremiumClaimed = data.ClaimedPremiumTiers.Contains(i);

            // Free-Reward kann beansprucht werden: Tier erreicht + noch nicht geclaimed
            bool canClaimFree = isUnlocked && !isFreeClaimed;

            // Premium-Reward: Tier erreicht + Premium aktiv + noch nicht geclaimed
            bool canClaimPremium = isUnlocked && data.IsPremium && !isPremiumClaimed;

            // Premium-Schloss: Tier erreicht, aber kein Premium und noch nicht geclaimed
            bool showPremiumLock = isUnlocked && !data.IsPremium && !isPremiumClaimed;

            var freeReward = _freeRewards[i];
            var premiumReward = _premiumRewards[i];

            items.Add(new BattlePassTierDisplayItem
            {
                TierIndex = i,
                TierNumber = (i + 1).ToString(),
                FreeRewardText = FormatRewardText(freeReward),
                FreeRewardIcon = freeReward.IconName,
                PremiumRewardText = FormatRewardText(premiumReward),
                PremiumRewardIcon = premiumReward.IconName,
                IsUnlocked = isUnlocked,
                IsCurrent = isCurrent,
                IsFreeClaimed = isFreeClaimed,
                IsPremiumClaimed = isPremiumClaimed,
                CanClaimFree = canClaimFree,
                CanClaimPremium = canClaimPremium,
                ShowPremiumLock = showPremiumLock,
                ClaimText = claimText,
                TierBadgeColor = isCurrent ? "#C0FFD700" : (isUnlocked ? "#60FFFFFF" : "#30FFFFFF"),
                DisplayOpacity = isUnlocked || isCurrent ? 1.0 : 0.5,
                TierBackground = isCurrent ? "#18FFD700" : (isUnlocked ? "#10FFFFFF" : "#08FFFFFF"),
                TierBorderColor = isCurrent ? "#60FFD700" : (canClaimFree || canClaimPremium ? "#4000FF88" : "#20FFFFFF")
            });
        }

        Tiers = items;
    }

    /// <summary>Nur die Texte in der Tier-Liste aktualisieren (ohne Neuaufbau)</summary>
    private void RefreshTierTexts()
    {
        if (Tiers.Count == 0) return;

        for (int i = 0; i < Tiers.Count && i < BattlePassTierDefinitions.MaxTier; i++)
        {
            Tiers[i].FreeRewardText = FormatRewardText(_freeRewards[i]);
            Tiers[i].PremiumRewardText = FormatRewardText(_premiumRewards[i]);
        }
    }

    /// <summary>XP-Boost-Zustand aktualisieren (Button-Text, Timer-Text)</summary>
    private void UpdateXpBoostState()
    {
        IsXpBoostActive = _battlePassService.IsXpBoostActive;

        if (IsXpBoostActive)
        {
            var activeFormat = _localizationService.GetString("XpBoostActive") ?? "Active! {0} remaining";
            XpBoostTimeText = string.Format(activeFormat, GetBoostRemainingText());
            XpBoostButtonText = _localizationService.GetString("XpBoostTitle") ?? "2x XP Boost";
        }
        else
        {
            XpBoostTimeText = "";
            var priceText = _localizationService.GetString("XpBoostPrice") ?? "20 Gems";
            XpBoostButtonText = $"{_localizationService.GetString("XpBoostTitle") ?? "2x XP Boost"} ({priceText})";
        }
    }

    /// <summary>Verbleibende Boost-Zeit als String (z.B. "23:45")</summary>
    private string GetBoostRemainingText()
    {
        var expires = _battlePassService.XpBoostExpiresAt;
        if (expires == null) return "";

        var remaining = expires.Value - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0) return "";

        return remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}:{remaining.Minutes:D2}h"
            : $"{remaining.Minutes}:{remaining.Seconds:D2}m";
    }

    /// <summary>
    /// Belohnungs-Text formatieren (z.B. "500 Coins", "3 Gems", "1 Karten-Pack").
    /// </summary>
    private string FormatRewardText(BattlePassReward reward)
    {
        return reward.Type switch
        {
            BattlePassRewardType.Coins => $"{reward.Amount:N0} Coins",

            BattlePassRewardType.Gems => $"{reward.Amount} Gems",

            BattlePassRewardType.CardPack => reward.Amount == 1
                ? $"1 {_localizationService.GetString("CardPack") ?? "Card Pack"}"
                : $"{reward.Amount} {_localizationService.GetString("CardPacks") ?? "Card Packs"}",

            BattlePassRewardType.Cosmetic =>
                _localizationService.GetString(reward.DescriptionKey) ?? reward.DescriptionKey,

            _ => reward.DescriptionKey
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// DISPLAY-MODEL
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Anzeige-Modell für ein einzelnes Battle-Pass-Tier in der View.
/// Enthält alle berechneten Werte für Free- und Premium-Track.
/// </summary>
public class BattlePassTierDisplayItem
{
    /// <summary>0-basierter Index</summary>
    public int TierIndex { get; init; }

    /// <summary>Anzeige-Nummer ("1", "2", ...)</summary>
    public string TierNumber { get; set; } = "";

    /// <summary>Formatierter Text der Free-Belohnung (z.B. "500 Coins")</summary>
    public string FreeRewardText { get; set; } = "";

    /// <summary>MaterialIconKind-Name für das Free-Reward-Icon</summary>
    public string FreeRewardIcon { get; set; } = "";

    /// <summary>Formatierter Text der Premium-Belohnung (z.B. "3 Gems")</summary>
    public string PremiumRewardText { get; set; } = "";

    /// <summary>MaterialIconKind-Name für das Premium-Reward-Icon</summary>
    public string PremiumRewardIcon { get; set; } = "";

    /// <summary>Ob das Tier erreicht/freigeschaltet wurde</summary>
    public bool IsUnlocked { get; set; }

    /// <summary>Ob dies das aktuell aktive Tier ist</summary>
    public bool IsCurrent { get; set; }

    /// <summary>Ob die Free-Belohnung bereits beansprucht wurde</summary>
    public bool IsFreeClaimed { get; set; }

    /// <summary>Ob die Premium-Belohnung bereits beansprucht wurde</summary>
    public bool IsPremiumClaimed { get; set; }

    /// <summary>Ob die Free-Belohnung jetzt beansprucht werden kann</summary>
    public bool CanClaimFree { get; set; }

    /// <summary>Ob die Premium-Belohnung jetzt beansprucht werden kann</summary>
    public bool CanClaimPremium { get; set; }

    /// <summary>Ob das Premium-Schloss angezeigt wird (kein Premium + nicht geclaimed + Tier erreicht)</summary>
    public bool ShowPremiumLock { get; set; }

    /// <summary>Lokalisierter Claim-Button-Text</summary>
    public string ClaimText { get; set; } = "Claim";

    /// <summary>Badge-Farbe für die Tier-Nummer (Gold=aktuell, Accent=freigeschaltet, Grau=gesperrt)</summary>
    public string TierBadgeColor { get; set; } = "#40FFFFFF";

    /// <summary>Opacity für gesperrte Tiers (0.5) vs freigeschaltete (1.0)</summary>
    public double DisplayOpacity { get; set; } = 1.0;

    /// <summary>Hintergrundfarbe (aktuelles Tier = Gold-Tint, freigeschaltet = heller)</summary>
    public string TierBackground { get; set; } = "#08FFFFFF";

    /// <summary>Rahmenfarbe (claimbar = Grün-Tint, aktuell = Gold)</summary>
    public string TierBorderColor { get; set; } = "#20FFFFFF";
}
