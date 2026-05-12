using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Eigenständiges ViewModel für alle Dialog-Typen:
/// Alert, Confirm, Story, Achievement, LevelUp, Hint, Prestige-Summary, Prestige-Tier-Auswahl.
/// Aufgeteilt in mehrere Partial-Files pro Dialog-Typ.
/// </summary>
public sealed partial class DialogViewModel : ViewModelBase, IDialogService
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

    // Dialog-Type-spezifische Properties + Commands liegen in Partial-Files:
    // - DialogViewModel.LevelUp.cs       — LevelUp-Dialog
    // - DialogViewModel.Achievement.cs   — Achievement-Dialog
    // - DialogViewModel.Story.cs         — Story-Dialog (AAA-Audit P0)
    // - DialogViewModel.Hint.cs          — Hint-Dialog (AAA-Audit P0)
    // - DialogViewModel.Alert.cs         — Alert-Dialog
    // - DialogViewModel.PrestigeSummary.cs — Post-Prestige-Summary
    // - DialogViewModel.PrestigeTier.cs  — Prestige-Tier-Auswahl (AAA-Audit P0, 12.05.2026)

    // ═══════════════════════════════════════════════════════════════════════
    // CONFIRM DIALOG (geteilt zwischen generischem Confirm und Prestige-Tier)
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
    // CONFIRM-DIALOG COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

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
            _localizationService.GetString("TabLockedHint") ?? "Unlocks at Level {0}",
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

    // PRESTIGE-TIER-LOGIK → DialogViewModel.PrestigeTier.cs (AAA-Audit P0, 12.05.2026)

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
        AlertDialogMessage = $"{level} ({rep.ReputationScore}/100)\n×{multiplier:F1} {_localizationService.GetString("IncomeBonus") ?? "Income Bonus"}";
        AlertDialogButtonText = "OK";
        IsAlertDialogVisible = true;
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
