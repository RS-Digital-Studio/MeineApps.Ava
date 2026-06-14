using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Economy-Geschäftslogik extrahiert aus MainViewModel.Economy.cs (03.04.2026).
/// Properties bleiben auf MainViewModel (AXAML-Bindings unverändert).
/// Zugriff auf MainViewModel-Properties über _host Referenz.
/// </summary>
/// <remarks>
/// Partial-Split (v2.1.4) — kohärente Economy-Geschäftslogik, aufgeteilt nach Sub-Bereichen:
/// <list type="bullet">
/// <item>EconomyFeatureViewModel.cs — Felder, Konstruktor, Dialog-Helfer, Daten-Akzessoren.</item>
/// <item>EconomyFeatureViewModel.Workshops.cs — Auswahl, Kauf, Upgrade, Hire, BulkBuy, Display-Aufbau.</item>
/// <item>EconomyFeatureViewModel.Orders.cs — Auftrags-Start/Resume/Material-Order, Order-Refresh.</item>
/// <item>EconomyFeatureViewModel.Boosts.cs — Feierabend-Rush, Lieferant, Boost-Indikator.</item>
/// <item>EconomyFeatureViewModel.Refresh.cs — Gesamt-Refresh State→UI, Gebäude/Feature-Status/Reputation.</item>
/// <item>EconomyFeatureViewModel.Prestige.cs — Prestige-Banner, Challenges, Tier-Badge.</item>
/// </list>
/// </remarks>
internal sealed partial class EconomyFeatureViewModel
{
    private readonly MainViewModel _host;
    private readonly IGameStateService _gameStateService;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly IOrderGeneratorService _orderGeneratorService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IPrestigeService _prestigeService;
    private readonly IChallengeConstraintService? _challengeConstraints;
    private readonly IContextualHintService _contextualHintService;
    private readonly IPurchaseService _purchaseService;
    private readonly IDailyChallengeService _dailyChallengeService;
    private readonly IWeeklyMissionService _weeklyMissionService;
    private readonly IEventService _eventService;
    private readonly IDialogService _dialogService;
    private readonly IAnalyticsService? _analyticsService;
    // V7 (): optional injiziert — Lager + Research-Bonus fuer Lieferungen.
    private readonly IWarehouseService? _warehouseService;
    private readonly IResearchService? _researchService;

    /// <summary>FloatingText im Dashboard anzeigen.</summary>
    internal event Action<string, string>? FloatingTextRequested;

    /// <summary>Confetti-Celebration auslösen.</summary>
    internal event Action? CelebrationRequested;

    internal EconomyFeatureViewModel(
        MainViewModel host,
        IGameStateService gameStateService,
        IAudioService audioService,
        ILocalizationService localizationService,
        IOrderGeneratorService orderGeneratorService,
        IRewardedAdService rewardedAdService,
        IPrestigeService prestigeService,
        IChallengeConstraintService? challengeConstraints,
        IContextualHintService contextualHintService,
        IPurchaseService purchaseService,
        IDailyChallengeService dailyChallengeService,
        IWeeklyMissionService weeklyMissionService,
        IEventService eventService,
        IDialogService dialogService,
        IAnalyticsService? analyticsService = null,
        IWarehouseService? warehouseService = null,
        IResearchService? researchService = null)
    {
        _host = host;
        _gameStateService = gameStateService;
        _audioService = audioService;
        _localizationService = localizationService;
        _orderGeneratorService = orderGeneratorService;
        _rewardedAdService = rewardedAdService;
        _prestigeService = prestigeService;
        _challengeConstraints = challengeConstraints;
        _contextualHintService = contextualHintService;
        _purchaseService = purchaseService;
        _dailyChallengeService = dailyChallengeService;
        _weeklyMissionService = weeklyMissionService;
        _eventService = eventService;
        _dialogService = dialogService;
        _analyticsService = analyticsService;
        _warehouseService = warehouseService;
        _researchService = researchService;
    }

    private bool ShowAds => !_purchaseService.IsPremium;

    private void ShowAlertDialog(string? title, string? message, string? button)
        => _dialogService.ShowAlertDialog(title ?? "", message ?? "", button ?? "OK");

    private Task<bool> ShowConfirmDialog(string title, string message, string accept, string cancel)
        => _dialogService.ShowConfirmDialog(title, message, accept, cancel);

    // Prestige-Banner Dirty-Flag: Nur neu berechnen wenn sich Level oder Prestige-Count aendert
    private int _lastPrestigeBannerLevel = -1;
    private int _lastPrestigeBannerPrestigeCount = -1;

    /// <summary>Prestige-Banner-Cache invalidieren (z.B. bei Sprachwechsel).</summary>
    internal void InvalidatePrestigeBannerCache() => _lastPrestigeBannerLevel = -1;
    // Wiederverwendbare Listen fuer Prestige-Banner (vermeidet 2x new List<string> alle 5 Ticks)
    private readonly List<string> _prestigeGains = new();
    private readonly List<string> _prestigeLosses = new();

    // ═══════════════════════════════════════════════════════════════════════
    // HOLD-TO-UPGRADE (stille Upgrades ohne Sound/FloatingText)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Flag: Hold-to-Upgrade aktiv → aufpoppende Dialoge unterdrücken.
    /// </summary>
    public bool IsHoldingUpgrade { get; set; }

    /// <summary>
    /// Stilles Upgrade ohne Sound/FloatingText - für Hold-to-Upgrade.
    /// </summary>
    public bool UpgradeWorkshopSilent(WorkshopType type)
    {
        return _gameStateService.TryUpgradeWorkshop(type);
    }

    /// <summary>
    /// Spielt den Upgrade-Sound ab (für Hold-to-Upgrade Ende).
    /// </summary>
    public void PlayUpgradeSound()
    {
        _audioService.PlaySoundAsync(GameSound.Upgrade).FireAndForget();
    }

    /// <summary>
    /// Aktualisiert eine einzelne Workshop-Anzeige (öffentlicher Zugang für Code-Behind).
    /// </summary>
    public void RefreshSingleWorkshopPublic(WorkshopType type)
    {
        RefreshSingleWorkshop(type);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DATEN-AKZESSOREN (für SkiaSharp-Rendering + Ladebildschirm)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gibt den aktuellen GameState für SkiaSharp-Rendering zurück (City-Skyline im Header).
    /// </summary>
    public GameState? GetGameStateForRendering()
    {
        return _gameStateService.State;
    }

    /// <summary>
    /// Gibt die lokalisierten Tab-Labels für die SkiaSharp Tab-Bar zurück.
    /// </summary>
    public string[] GetTabLabels() =>
    [
        _localizationService.GetString("TabWerkstatt") ?? "Workshop",
        _localizationService.GetString("TabImperium") ?? "Empire",
        _localizationService.GetString("TabMissionen") ?? "Missions",
        _localizationService.GetString("TabGilde") ?? "Guild",
        _localizationService.GetString("TabShop") ?? "Shop"
    ];

    /// <summary>
    /// Gibt die lokalisierten Loading-Tipps für den Ladebildschirm zurück.
    /// </summary>
    public string[] GetLoadingTips() =>
    [
        _localizationService.GetString("LoadingTip1") ?? "Tip: Hold the upgrade button for rapid leveling!",
        _localizationService.GetString("LoadingTip2") ?? "Tip: Higher worker tiers earn significantly more!",
        _localizationService.GetString("LoadingTip3") ?? "Tip: Visit daily for login rewards!",
        _localizationService.GetString("LoadingTip4") ?? "Tip: Prestige unlocks new bonuses and workshops!",
        _localizationService.GetString("LoadingTip5") ?? "Tip: Reputation above 70 brings extra orders!",
        _localizationService.GetString("LoadingTip6") ?? "Tip: Master tools give permanent income bonuses!"
    ];
}
