using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Eigenständiges ViewModel für alle Dialog-Typen:
/// Alert, Confirm, Story, Achievement, LevelUp, Hint, Prestige-Summary, Prestige-Tier-Auswahl.
/// Extrahiert aus MainViewModel zur Reduktion der Klassengröße.
/// </summary>
public sealed partial class DialogViewModel : ObservableObject, IDialogService
{
    private readonly ILocalizationService _localizationService;
    private readonly IStoryService? _storyService;
    private readonly IContextualHintService _contextualHintService;
    private readonly IGameStateService _gameStateService;
    private readonly IPrestigeService _prestigeService;
    private readonly IAdService _adService;
    private readonly IChallengeConstraintService? _challengeConstraints;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS (für Kommunikation mit MainViewModel)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Wird ausgelöst wenn ein Dialog geschlossen wird und aufgeschobene Dialoge geprüft werden sollen.</summary>
    public event Action? DeferredDialogCheckRequested;

    /// <summary>Wird ausgelöst wenn der Prestige-Summary-Dialog "Zum Shop" gedrückt wird.</summary>
    public event Action? PrestigeSummaryGoToShopRequested;

    /// <summary>Wird ausgelöst wenn ein FloatingText angezeigt werden soll.</summary>
    public event Action<string, string>? FloatingTextRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // LEVEL-UP DIALOG
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isLevelUpDialogVisible;

    [ObservableProperty]
    private bool _isLevelUpPulsing;

    [ObservableProperty]
    private int _levelUpNewLevel;

    [ObservableProperty]
    private string _levelUpUnlockedText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // ACHIEVEMENT DIALOG
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isAchievementDialogVisible;

    [ObservableProperty]
    private string _achievementName = "";

    [ObservableProperty]
    private string _achievementDescription = "";

    // ═══════════════════════════════════════════════════════════════════════
    // STORY-DIALOG (Meister Hans NPC)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isStoryDialogVisible;

    [ObservableProperty]
    private string _storyTitle = "";

    [ObservableProperty]
    private string _storyText = "";

    [ObservableProperty]
    private string _storyMood = "happy";

    [ObservableProperty]
    private string _storyRewardText = "";

    [ObservableProperty]
    private string _storyChapterId = "";

    [ObservableProperty]
    private bool _hasNewStory;

    [ObservableProperty]
    private int _storyChapterNumber;

    [ObservableProperty]
    private int _storyTotalChapters = 25;

    [ObservableProperty]
    private bool _isStoryTutorial;

    [ObservableProperty]
    private string _storyChapterBadge = "";

    // ═══════════════════════════════════════════════════════════════════════
    // KONTEXTUELLER HINT (Tooltip-Bubbles / Dialog)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isHintVisible;

    [ObservableProperty]
    private string _activeHintTitle = "";

    [ObservableProperty]
    private string _activeHintText = "";

    /// <summary>
    /// True wenn der Hint als zentrierter Dialog angezeigt wird (z.B. Welcome).
    /// </summary>
    [ObservableProperty]
    private bool _isHintDialog;

    /// <summary>
    /// True wenn Tooltip-Bubble oben positioniert ist (HintPosition.Below -> Bubble zeigt von oben nach unten).
    /// </summary>
    [ObservableProperty]
    private bool _isHintTooltipAbove;

    /// <summary>
    /// True wenn Tooltip-Bubble unten positioniert ist (HintPosition.Above -> Bubble zeigt von unten nach oben).
    /// </summary>
    [ObservableProperty]
    private bool _isHintTooltipBelow;

    [ObservableProperty]
    private string _hintDismissButtonText = "Verstanden";

    // ═══════════════════════════════════════════════════════════════════════
    // ALERT DIALOG
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isAlertDialogVisible;

    [ObservableProperty]
    private string _alertDialogTitle = "";

    [ObservableProperty]
    private string _alertDialogMessage = "";

    [ObservableProperty]
    private string _alertDialogButtonText = "OK";

    // ═══════════════════════════════════════════════════════════════════════
    // CONFIRM DIALOG
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isConfirmDialogVisible;

    [ObservableProperty]
    private string _confirmDialogTitle = "";

    [ObservableProperty]
    private string _confirmDialogMessage = "";

    [ObservableProperty]
    private string _confirmDialogAcceptText = "OK";

    [ObservableProperty]
    private string _confirmDialogCancelText = "";

    private TaskCompletionSource<bool>? _confirmDialogTcs;

    /// <summary>Ob die Tier-Auswahl-Chips im Bestätigungsdialog sichtbar sind.</summary>
    [ObservableProperty]
    private bool _isPrestigeTierSelectionVisible;

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE-SUMMARY DIALOG
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isPrestigeSummaryVisible;

    [ObservableProperty]
    private string _prestigeSummaryTier = "";

    [ObservableProperty]
    private string _prestigeSummaryTierIcon = "";

    [ObservableProperty]
    private string _prestigeSummaryTierColor = "#FFD700";

    [ObservableProperty]
    private string _prestigeSummaryPoints = "";

    [ObservableProperty]
    private string _prestigeSummaryMultiplier = "";

    [ObservableProperty]
    private string _prestigeSummaryCount = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE-TIER AUSWAHL
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Ob mehrere Prestige-Tiers verfügbar sind (für Tier-Auswahl im Dialog).</summary>
    [ObservableProperty]
    private bool _hasMultiplePrestigeTiers;

    /// <summary>Aktuell ausgewählter Tier im Bestätigungsdialog (Index in AvailableTiers).</summary>
    [ObservableProperty]
    private int _selectedPrestigeTierIndex;

    /// <summary>Tier-Auswahl-Chips für den Dialog.</summary>
    [ObservableProperty]
    private List<PrestigeTierOption> _availablePrestigeTierOptions = [];

    /// <summary>Merkt sich den aktuell ausgewählten Tier im Prestige-Dialog.</summary>
    private PrestigeTier _dialogSelectedTier = PrestigeTier.None;

    /// <summary>Zugriff auf den gewählten Tier für MainViewModel nach Bestätigung.</summary>
    public PrestigeTier DialogSelectedTier => _dialogSelectedTier;

    /// <summary>Bonus-PP-Vorschau aus Spielleistung (flat).</summary>
    [ObservableProperty]
    private string _bonusPpPreview = string.Empty;

    /// <summary>Challenge-PP-Vorschau (additiver Bonus).</summary>
    [ObservableProperty]
    private string _challengePpPreview = string.Empty;

    /// <summary>Aktuelle Run-Dauer (für Speedrun-Anzeige im Dialog).</summary>
    [ObservableProperty]
    private string _currentRunDurationText = string.Empty;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public DialogViewModel(
        ILocalizationService localizationService,
        IStoryService? storyService,
        IContextualHintService contextualHintService,
        IGameStateService gameStateService,
        IPrestigeService prestigeService,
        IAdService adService,
        IChallengeConstraintService? challengeConstraints = null)
    {
        _localizationService = localizationService;
        _storyService = storyService;
        _contextualHintService = contextualHintService;
        _gameStateService = gameStateService;
        _prestigeService = prestigeService;
        _adService = adService;
        _challengeConstraints = challengeConstraints;

        // Kontextuelles Hint-System verdrahten
        _contextualHintService.HintChanged += OnHintChanged;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IsAnyDialogVisible
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// True wenn irgendein Overlay-Dialog sichtbar ist.
    /// Verhindert dass Hints oder andere Dialoge gleichzeitig erscheinen.
    /// Prüft NICHT IsOfflineEarningsDialogVisible/IsDailyRewardDialogVisible etc. --
    /// diese verbleiben im MainViewModel und werden dort separat gecheckt.
    /// </summary>
    public bool IsAnyDialogVisible =>
        IsStoryDialogVisible || IsHintVisible ||
        IsLevelUpDialogVisible || IsAchievementDialogVisible ||
        IsAlertDialogVisible || IsConfirmDialogVisible ||
        IsPrestigeSummaryVisible;

    // ═══════════════════════════════════════════════════════════════════════
    // DISMISS COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void DismissLevelUpDialog()
    {
        IsLevelUpDialogVisible = false;
    }

    [RelayCommand]
    private void DismissAchievementDialog()
    {
        IsAchievementDialogVisible = false;
    }

    [RelayCommand]
    private void DismissAlertDialog()
    {
        IsAlertDialogVisible = false;
    }

    [RelayCommand]
    private void DismissPrestigeSummary()
    {
        IsPrestigeSummaryVisible = false;
    }

    [RelayCommand]
    private void PrestigeSummaryGoToShop()
    {
        IsPrestigeSummaryVisible = false;
        PrestigeSummaryGoToShopRequested?.Invoke();
    }

    [RelayCommand]
    private void ConfirmDialogAccept()
    {
        IsConfirmDialogVisible = false;
        IsPrestigeTierSelectionVisible = false;
        _confirmDialogTcs?.TrySetResult(true);
    }

    [RelayCommand]
    private void ConfirmDialogCancel()
    {
        IsConfirmDialogVisible = false;
        IsPrestigeTierSelectionVisible = false;
        _confirmDialogTcs?.TrySetResult(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ALERT / CONFIRM DIALOG
    // ═══════════════════════════════════════════════════════════════════════

    public void ShowAlertDialog(string title, string message, string buttonText)
    {
        AlertDialogTitle = title;
        AlertDialogMessage = message;
        AlertDialogButtonText = buttonText;
        IsAlertDialogVisible = true;
    }

    /// <summary>
    /// Zeigt einen FloatingText-Hinweis wenn ein gesperrter Tab angetippt wird.
    /// </summary>
    public void ShowLockedTabHint(int requiredLevel)
    {
        var text = string.Format(
            _localizationService.GetString("TabLockedHint") ?? "Ab Level {0} verfügbar",
            requiredLevel);
        FloatingTextRequested?.Invoke(text, "info");
    }

    public Task<bool> ShowConfirmDialog(string title, string message, string acceptText, string cancelText)
    {
        ConfirmDialogTitle = title;
        ConfirmDialogMessage = message;
        ConfirmDialogAcceptText = acceptText;
        ConfirmDialogCancelText = cancelText;
        IsPrestigeTierSelectionVisible = false;
        _confirmDialogTcs = new TaskCompletionSource<bool>();
        IsConfirmDialogVisible = true;

        // Ad-Banner ausblenden damit es nicht den Dialog verdeckt
        _adService.HideBanner();

        return _confirmDialogTcs.Task;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE-BESTÄTIGUNG (Dialog-Teil)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tier-Auswahl im Prestige-Dialog: Aktualisiert die Vorschau beim Wechsel.
    /// Parameter ist der Tier-Name als String (z.B. "Bronze", "Silver").
    /// </summary>
    [RelayCommand]
    private void SelectPrestigeTier(string tierName)
    {
        if (!Enum.TryParse<PrestigeTier>(tierName, out var tier)) return;

        var idx = AvailablePrestigeTierOptions.FindIndex(o => o.Tier == tier);
        if (idx < 0) return;

        SelectedPrestigeTierIndex = idx;

        // IsSelected aktualisieren
        for (int i = 0; i < AvailablePrestigeTierOptions.Count; i++)
            AvailablePrestigeTierOptions[i].IsSelected = i == idx;
        OnPropertyChanged(nameof(AvailablePrestigeTierOptions));

        _dialogSelectedTier = tier;

        // Dialog-Inhalt dynamisch aktualisieren
        UpdatePrestigeDialogContent(tier);
    }

    /// <summary>
    /// Baut die Dialog-Texte für einen bestimmten Tier auf.
    /// Zeigt Gewinne, Bewahrung, detaillierte Verluste und Timing-Warnung.
    /// </summary>
    public void UpdatePrestigeDialogContent(PrestigeTier tier)
    {
        var state = _gameStateService.State;
        var tierName = _localizationService.GetString(tier.GetLocalizationKey()) ?? tier.ToString();
        int tierPoints = CalculateEffectivePrestigePoints(state, tier);

        // Startgeld für den gewählten Tier (Tier-Basis + Shop-Boni, identisch mit ResetProgress)
        var startMoney = tier.GetTierStartMoney();
        foreach (var shopItem in PrestigeShop.GetAllItems())
        {
            if (state.Prestige.PurchasedShopItems.Contains(shopItem.Id) && shopItem.Effect.ExtraStartMoney > 0)
                startMoney += shopItem.Effect.ExtraStartMoney;
        }

        // 1. GEWINNE (prominent, oben)
        var gains = new List<string>();
        gains.Add($"\u2b06 +{tierPoints} PP ({tierName} x{tier.GetPointMultiplier()})");
        gains.Add($"\u2b06 +{tier.GetPermanentMultiplierBonus():P0} {_localizationService.GetString("PermanentIncomeBonus") ?? "permanenter Einkommens-Bonus"}");
        gains.Add($"\u2b06 {_localizationService.GetString("StartMoney") ?? "Startgeld"}: {MoneyFormatter.FormatCompact(startMoney)}");

        // Speed-Up Prognose
        decimal currentMult = state.Prestige.PermanentMultiplier;
        decimal newMult = currentMult + tier.GetPermanentMultiplierBonus();
        if (currentMult > 0)
        {
            int speedUp = (int)((newMult / currentMult - 1m) * 100);
            gains.Add($"\u26a1 ~{speedUp}% {_localizationService.GetString("Faster") ?? "schneller als vorher"}");
        }

        // 2. BEWAHRUNG (positiv formuliert)
        if (tier.KeepsResearch())
            gains.Add($"\u2713 {_localizationService.GetString("PrestigeKeepsResearch") ?? "Forschung bleibt erhalten"}");
        if (tier.KeepsMasterTools())
            gains.Add($"\u2713 {_localizationService.GetString("PrestigeKeepsTools") ?? "Meisterwerkzeuge bleiben"}");
        if (tier.KeepsBuildings())
            gains.Add($"\u2713 {_localizationService.GetString("PrestigeKeepsBuildings") ?? "Gebäude bleiben (Lv.1)"}");
        if (tier.KeepsManagers())
            gains.Add($"\u2713 {_localizationService.GetString("PrestigeKeepsManagers") ?? "Manager bleiben (Lv.1)"}");
        if (tier.KeepsBestWorkers())
            gains.Add($"\u2713 {_localizationService.GetString("PrestigeKeepsWorkers") ?? "Beste Worker pro Workshop bleiben"}");

        // 3. VERLUSTE (detailliert, explizit benannt)
        var losses = new List<string>();
        losses.Add($"\u2716 {_localizationService.GetString("PrestigeLossLevel") ?? "Level und XP"}");
        losses.Add($"\u2716 {_localizationService.GetString("PrestigeLossMoney") ?? "Geld und Aufträge"}");
        losses.Add($"\u2716 {_localizationService.GetString("PrestigeLossWorkshops") ?? "Workshops (nur Schreinerei bleibt)"}");

        if (!tier.KeepsResearch())
            losses.Add($"\u2716 {_localizationService.GetString("PrestigeLossResearch") ?? "Forschung"}");
        if (!tier.KeepsMasterTools())
            losses.Add($"\u2716 {_localizationService.GetString("PrestigeLossTools") ?? "Meisterwerkzeuge"}");
        if (!tier.KeepsBuildings())
            losses.Add($"\u2716 {_localizationService.GetString("PrestigeLossBuildings") ?? "Gebäude"}");
        if (!tier.KeepsEquipment())
            losses.Add($"\u2716 {_localizationService.GetString("PrestigeLossEquipment") ?? "Ausrüstung"}");
        if (!tier.KeepsManagers())
            losses.Add($"\u2716 {_localizationService.GetString("PrestigeLossManagers") ?? "Vorarbeiter"}");

        // Immer verloren (unabhängig vom Tier)
        losses.Add($"\u2716 {_localizationService.GetString("PrestigeLossCrafting") ?? "Crafting-Inventar und Aufträge"}");

        // 4. TIMING-WARNUNG (suboptimales Prestige erkennen)
        string timingWarning = "";
        var nextTier = tier.GetNextTier();
        if (nextTier != PrestigeTier.None)
        {
            int currentLevel = state.PlayerLevel;
            int nextTierLevel = nextTier.GetRequiredLevel();

            bool wouldNextTierBeAvailable = currentLevel >= nextTierLevel;
            if (wouldNextTierBeAvailable)
            {
                int currentTierCountAfterPrestige = tier switch
                {
                    PrestigeTier.Bronze => state.Prestige.BronzeCount + 1,
                    PrestigeTier.Silver => state.Prestige.SilverCount + 1,
                    PrestigeTier.Gold => state.Prestige.GoldCount + 1,
                    PrestigeTier.Platin => state.Prestige.PlatinCount + 1,
                    PrestigeTier.Diamant => state.Prestige.DiamantCount + 1,
                    PrestigeTier.Meister => state.Prestige.MeisterCount + 1,
                    _ => 0
                };
                wouldNextTierBeAvailable = currentTierCountAfterPrestige >= nextTier.GetRequiredPreviousTierCount();
            }

            if (wouldNextTierBeAvailable)
            {
                int nextTierPoints = CalculateEffectivePrestigePoints(state, nextTier);
                timingWarning = $"\n\u26a0 {string.Format(
                    _localizationService.GetString("PrestigeTimingWarning") ?? "Du bist schon Level {0} - ab Level {1} wäre {2} möglich (mehr PP)!",
                    currentLevel, nextTierLevel,
                    _localizationService.GetString(nextTier.GetLocalizationKey()) ?? nextTier.ToString())}";
            }
            else if (currentLevel >= (int)(nextTierLevel * 0.7))
            {
                var nextTierName = _localizationService.GetString(nextTier.GetLocalizationKey()) ?? nextTier.ToString();
                timingWarning = $"\n\u2139 {string.Format(
                    _localizationService.GetString("PrestigeNearNextTier") ?? "Noch {0} Level bis {1} (Level {2}) - weiterspielen lohnt sich!",
                    nextTierLevel - currentLevel, nextTierName, nextTierLevel)}";
            }
        }

        // 5. BONUS-PP + CHALLENGES
        int bonusPp = _prestigeService.CalculateBonusPrestigePoints(tier);
        BonusPpPreview = bonusPp > 0
            ? $"+{bonusPp} PP ({_localizationService.GetString("BonusPP") ?? "Bonus"})"
            : string.Empty;

        decimal challengeMult = _challengeConstraints?.GetChallengePpMultiplier() ?? 1.0m;
        ChallengePpPreview = challengeMult > 1.0m
            ? $"x{challengeMult:F2} ({_localizationService.GetString("Challenges") ?? "Herausforderungen"})"
            : string.Empty;

        // 6. SPEEDRUN-TIMER
        var runDuration = _prestigeService.GetCurrentRunDuration();
        CurrentRunDurationText = runDuration.HasValue
            ? $"{runDuration.Value.Hours}h {runDuration.Value.Minutes:D2}m"
            : string.Empty;

        ConfirmDialogTitle = $"{_localizationService.GetString("Prestige") ?? "Prestige"} \u2192 {tierName}";
        ConfirmDialogMessage = string.Join("\n", gains)
            + $"\n\n{string.Join("\n", losses)}"
            + timingWarning;
    }

    /// <summary>
    /// Bereitet den Prestige-Bestätigungsdialog vor und zeigt ihn an.
    /// Liefert true bei Bestätigung, false bei Abbruch.
    /// Die eigentliche Prestige-Durchführung bleibt im MainViewModel.
    /// </summary>
    public async Task<(bool confirmed, PrestigeTier selectedTier)> ShowPrestigeConfirmationDialogAsync()
    {
        var state = _gameStateService.State;
        var availableTiers = state.Prestige.GetAllAvailableTiers(state.PlayerLevel);

        if (availableTiers.Count == 0)
        {
            var minLevel = PrestigeTier.Bronze.GetRequiredLevel();
            ShowAlertDialog(
                _localizationService.GetString("PrestigeNotAvailable") ?? "Prestige nicht verfügbar",
                string.Format(
                    _localizationService.GetString("PrestigeNotAvailableDesc") ?? "Du benötigst Level {0} (aktuell Level {1})",
                    minLevel, state.PlayerLevel),
                _localizationService.GetString("OK") ?? "OK");
            return (false, PrestigeTier.None);
        }

        // Höchster Tier als Standard vorauswählen
        var highestTier = availableTiers[^1];
        _dialogSelectedTier = highestTier;

        // Tier-Optionen für Auswahl-Chips aufbauen
        var options = new List<PrestigeTierOption>();
        for (int i = 0; i < availableTiers.Count; i++)
        {
            var t = availableTiers[i];
            var tName = _localizationService.GetString(t.GetLocalizationKey()) ?? t.ToString();
            int tPoints = CalculateEffectivePrestigePoints(state, t);
            var preservations = new List<string>();
            if (t.KeepsResearch()) preservations.Add(_localizationService.GetString("Research") ?? "Forschung");
            if (t.KeepsMasterTools()) preservations.Add(_localizationService.GetString("MasterTools") ?? "Werkzeuge");
            if (t.KeepsBuildings()) preservations.Add(_localizationService.GetString("Buildings") ?? "Gebäude");
            if (t.KeepsManagers()) preservations.Add(_localizationService.GetString("Managers") ?? "Manager");
            if (t.KeepsBestWorkers()) preservations.Add(_localizationService.GetString("Workers") ?? "Worker");

            options.Add(new PrestigeTierOption
            {
                Tier = t,
                Name = tName,
                Icon = t.GetIcon(),
                Color = t.GetColorKey(),
                Points = tPoints,
                PointsText = $"+{tPoints} PP",
                BonusText = $"+{t.GetPermanentMultiplierBonus():P0}",
                PreservationText = preservations.Count > 0 ? string.Join(", ", preservations) : "",
                IsSelected = t == highestTier,
                IsRecommended = t == highestTier,
            });
        }
        AvailablePrestigeTierOptions = options;
        SelectedPrestigeTierIndex = availableTiers.Count - 1;
        HasMultiplePrestigeTiers = availableTiers.Count > 1;

        // Dialog-Inhalt für den höchsten Tier aufbauen
        UpdatePrestigeDialogContent(highestTier);

        // Dialog anzeigen und auf Ergebnis warten
        ConfirmDialogAcceptText = _localizationService.GetString("PrestigeConfirm") ?? "Prestige durchführen";
        ConfirmDialogCancelText = _localizationService.GetString("Cancel") ?? "Abbrechen";
        IsPrestigeTierSelectionVisible = HasMultiplePrestigeTiers;

        _confirmDialogTcs = new TaskCompletionSource<bool>();
        IsConfirmDialogVisible = true;
        _adService.HideBanner();

        var confirmed = await _confirmDialogTcs.Task;

        // Tier-Optionen zurücksetzen
        HasMultiplePrestigeTiers = false;
        AvailablePrestigeTierOptions = [];

        return (confirmed, _dialogSelectedTier);
    }

    /// <summary>
    /// Zeigt die Post-Prestige Zusammenfassung mit PP, Multiplikator und Shop-Link.
    /// </summary>
    public void ShowPrestigeSummary(PrestigeTier tier, int pointsEarned)
    {
        var state = _gameStateService.State;
        var tierName = _localizationService.GetString(tier.GetLocalizationKey()) ?? tier.ToString();

        PrestigeSummaryTier = tierName;
        PrestigeSummaryTierIcon = tier.GetIcon();
        PrestigeSummaryTierColor = tier.GetColorKey();
        PrestigeSummaryPoints = $"+{pointsEarned} PP";
        PrestigeSummaryMultiplier = $"{state.Prestige.PermanentMultiplier:F1}x";

        // Tier-spezifischer Count
        var count = tier switch
        {
            PrestigeTier.Bronze => state.Prestige.BronzeCount,
            PrestigeTier.Silver => state.Prestige.SilverCount,
            PrestigeTier.Gold => state.Prestige.GoldCount,
            PrestigeTier.Platin => state.Prestige.PlatinCount,
            PrestigeTier.Diamant => state.Prestige.DiamantCount,
            PrestigeTier.Meister => state.Prestige.MeisterCount,
            PrestigeTier.Legende => state.Prestige.LegendeCount,
            _ => state.Prestige.TotalPrestigeCount
        };
        PrestigeSummaryCount = $"#{count}";

        IsPrestigeSummaryVisible = true;
    }

    /// <summary>
    /// Berechnet die effektiven Prestige-Punkte inklusive aller Boni (identisch mit DoPrestige-Logik).
    /// Bronze-Minimum, Prestige-Pass (+50%), Gilden-Forschung (+10%) werden berücksichtigt.
    /// </summary>
    public int CalculateEffectivePrestigePoints(GameState state, PrestigeTier tier)
    {
        int basePoints = _prestigeService.GetPrestigePoints(state.CurrentRunMoney);
        int tierPoints = (int)(basePoints * tier.GetPointMultiplier());

        // Bronze: Mindestens 10 PP
        if (tier == PrestigeTier.Bronze && tierPoints < 10)
            tierPoints = 10;

        // Prestige-Pass: +50%
        if (state.IsPrestigePassActive)
            tierPoints = (int)(tierPoints * 1.5m);

        // Gilden-Forschung: Prestige-Punkte-Bonus (+10%)
        if (state.GuildMembership?.ResearchPrestigePointBonus > 0)
            tierPoints = (int)(tierPoints * (1m + state.GuildMembership.ResearchPrestigePointBonus));

        return tierPoints;
    }

    /// <summary>
    /// Zeigt Reputations-Info-Dialog mit Level und Multiplikator.
    /// </summary>
    [RelayCommand]
    private void ShowReputationInfo()
    {
        var rep = _gameStateService.State.Reputation;
        var level = _localizationService.GetString(rep.ReputationLevelKey)
                    ?? rep.ReputationLevelKey;
        var multiplier = rep.ReputationMultiplier;
        AlertDialogTitle = _localizationService.GetString("Reputation") ?? "Reputation";
        AlertDialogMessage = $"{level} ({rep.ReputationScore}/100)\n\u00d7{multiplier:F1} {_localizationService.GetString("IncomeBonus") ?? "Einkommensbonus"}";
        AlertDialogButtonText = "OK";
        IsAlertDialogVisible = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STORY-DIALOG COMMANDS (Meister Hans NPC)
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void DismissStoryDialog()
    {
        if (!string.IsNullOrEmpty(StoryChapterId))
        {
            _storyService?.MarkChapterViewed(StoryChapterId);
            HasNewStory = false;
        }
        IsStoryDialogVisible = false;
        DeferredDialogCheckRequested?.Invoke();

        // FloatingText für Belohnungen
        if (!string.IsNullOrEmpty(StoryRewardText))
        {
            FloatingTextRequested?.Invoke(StoryRewardText, "golden_screws");
        }
    }

    /// <summary>
    /// Prüft ob ein neues Story-Kapitel freigeschaltet wurde.
    /// Wird nach Level-Up, Workshop-Upgrade und Auftragsabschluss aufgerufen.
    /// </summary>
    public void CheckForNewStoryChapter(bool isAnyDialogVisible, bool isHoldingUpgrade)
    {
        var chapter = _storyService?.CheckForNewChapter();
        if (chapter != null)
        {
            HasNewStory = true;
            Dispatcher.UIThread.Post(() =>
            {
                if (!isAnyDialogVisible && !IsAnyDialogVisible && !isHoldingUpgrade)
                {
                    ShowStoryDialog(chapter);
                }
            }, DispatcherPriority.Background);
        }
    }

    private void ShowStoryDialog(StoryChapter chapter)
    {
        var title = _localizationService.GetString(chapter.TitleKey);
        var text = _localizationService.GetString(chapter.TextKey);

        StoryTitle = string.IsNullOrEmpty(title) ? chapter.TitleFallback : title;
        StoryText = string.IsNullOrEmpty(text) ? chapter.TextFallback : text;
        StoryMood = chapter.Mood;
        StoryChapterId = chapter.Id;
        StoryChapterNumber = chapter.ChapterNumber;
        StoryTotalChapters = 25;
        IsStoryTutorial = chapter.IsTutorial;
        StoryChapterBadge = chapter.IsTutorial
            ? _localizationService.GetString("StoryTipFromHans") ?? "Tipp von Meister Hans"
            : $"Kap. {chapter.ChapterNumber}/25";

        // Belohnungs-Text zusammenstellen (skalierte Geldbelohnung anzeigen)
        var rewards = new List<string>();
        if (chapter.MoneyReward > 0)
        {
            var netIncome = _gameStateService.State.NetIncomePerSecond;
            var scaledReward = Math.Max(chapter.MoneyReward, netIncome * 600);
            rewards.Add($"+{MoneyFormatter.FormatCompact(scaledReward)}");
        }
        if (chapter.GoldenScrewReward > 0)
        {
            var screwsLabel = _localizationService.GetString("GoldenScrews") ?? "Goldschrauben";
            rewards.Add($"+{chapter.GoldenScrewReward} {screwsLabel}");
        }
        if (chapter.XpReward > 0)
            rewards.Add($"+{chapter.XpReward} XP");
        StoryRewardText = string.Join("  |  ", rewards);

        IsStoryDialogVisible = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KONTEXTUELLER HINT COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void DismissHint()
    {
        var currentHintId = _contextualHintService.ActiveHint?.Id;
        _contextualHintService.DismissHint();

        // Nach Welcome-Dialog -> FirstWorkshop-Hint zeigen (Kette)
        if (currentHintId == ContextualHints.Welcome.Id)
        {
            _contextualHintService.TryShowHint(ContextualHints.FirstWorkshop);
        }

        DeferredDialogCheckRequested?.Invoke();
    }

    /// <summary>
    /// Reagiert auf HintChanged-Event vom ContextualHintService.
    /// Aktualisiert die UI-Properties für die Tooltip-Bubble / den Dialog.
    /// </summary>
    private void OnHintChanged(object? sender, ContextualHint? hint)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (hint == null)
            {
                IsHintVisible = false;
                IsHintDialog = false;
                IsHintTooltipAbove = false;
                IsHintTooltipBelow = false;
                return;
            }

            ActiveHintTitle = _localizationService.GetString(hint.TitleKey) ?? hint.TitleKey;
            ActiveHintText = _localizationService.GetString(hint.TextKey) ?? hint.TextKey;
            HintDismissButtonText = _localizationService.GetString("HintDismissButton") ?? "Verstanden";

            IsHintDialog = hint.IsDialog;
            IsHintTooltipAbove = !hint.IsDialog && hint.Position == HintPosition.Below;
            IsHintTooltipBelow = !hint.IsDialog && hint.Position == HintPosition.Above;

            IsHintVisible = true;
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CLEANUP
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Event-Subscriptions aufräumen. Wird von MainViewModel.Dispose() aufgerufen.
    /// </summary>
    public void Cleanup()
    {
        _contextualHintService.HintChanged -= OnHintChanged;
    }
}
