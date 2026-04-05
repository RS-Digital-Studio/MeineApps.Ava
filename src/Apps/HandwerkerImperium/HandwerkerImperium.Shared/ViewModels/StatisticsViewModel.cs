using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.ViewModels;
using MeineApps.Core.Premium.Ava.Services;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel for the statistics page.
/// Displays comprehensive game statistics and player progress.
/// </summary>
public sealed partial class StatisticsViewModel : ViewModelBase, INavigable
{
    private readonly IGameStateService _gameStateService;
    private readonly IPrestigeService _prestigeService;
    private readonly IAudioService _audioService;
    private readonly ILocalizationService _localizationService;
    private readonly IPurchaseService _purchaseService;
    private readonly IPlayGamesService? _playGamesService;
    private readonly IGameLoopService _gameLoopService;
    private readonly IDialogService _dialogService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PLAYER STATISTICS
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _playerLevel;

    [ObservableProperty]
    private int _totalXp;

    [ObservableProperty]
    private string _totalPlayTime = "0h 0m";

    [ObservableProperty]
    private string _totalMoneyEarned = "0 €";

    [ObservableProperty]
    private string _totalMoneySpent = "0 €";

    [ObservableProperty]
    private string _currentBalance = "0 €";

    // ═══════════════════════════════════════════════════════════════════════
    // ORDERS & MINIGAMES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _totalOrdersCompleted;

    [ObservableProperty]
    private int _totalMiniGamesPlayed;

    [ObservableProperty]
    private int _perfectRatings;

    [ObservableProperty]
    private int _currentPerfectStreak;

    [ObservableProperty]
    private int _bestPerfectStreak;

    [ObservableProperty]
    private double _perfectRate;

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _prestigeLevel;

    [ObservableProperty]
    private string _prestigeMultiplier = "1.0x";

    [ObservableProperty]
    private bool _canPrestige;

    [ObservableProperty]
    private string _potentialBonus = "+0%";

    [ObservableProperty]
    private int _minimumPrestigeLevel;

    /// <summary>
    /// Localized text for minimum prestige level requirement.
    /// </summary>
    public string MinLevelRequiredText => string.Format(
        _localizationService.GetString("MinLevelRequired"),
        MinimumPrestigeLevel);

    // ═══════════════════════════════════════════════════════════════════════
    // WORKSHOPS
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private List<WorkshopStatistic> _workshopStats = [];

    [ObservableProperty]
    private int _totalWorkshops;

    [ObservableProperty]
    private int _totalWorkers;

    [ObservableProperty]
    private string _totalIncomePerSecond = "0 €/s";

    /// <summary>
    /// Whether there are any workshop stats to show.
    /// </summary>
    public bool HasWorkshopStats => WorkshopStats.Count > 0;

    partial void OnWorkshopStatsChanged(List<WorkshopStatistic> value) => OnPropertyChanged(nameof(HasWorkshopStats));

    /// <summary>
    /// Ob Werbung angezeigt werden soll (nicht Premium).
    /// </summary>
    public bool ShowAds => !_purchaseService.IsPremium;

    /// <summary>
    /// Ob Play Games verfügbar ist (angemeldet).
    /// </summary>
    [ObservableProperty]
    private bool _isPlayGamesAvailable;

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE PASS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ob der Prestige-Pass für den aktuellen Durchlauf aktiv ist.
    /// </summary>
    [ObservableProperty]
    private bool _isPrestigePassActive;

    /// <summary>
    /// Ob der Prestige-Pass kaufbar ist (nicht bereits aktiv).
    /// </summary>
    [ObservableProperty]
    private bool _canBuyPrestigePass;

    // ═══════════════════════════════════════════════════════════════════════
    // FORTSCHRITT ZUM NÄCHSTEN TIER
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ob ein nächster Tier existiert (nicht auf Legende oder höher).
    /// </summary>
    [ObservableProperty]
    private bool _hasNextTier;

    /// <summary>
    /// Name des nächsten Tiers (lokalisiert).
    /// </summary>
    [ObservableProperty]
    private string _nextTierName = "";

    /// <summary>
    /// Icon des nächsten Tiers (Material Icon Kind).
    /// </summary>
    [ObservableProperty]
    private string _nextTierIcon = "";

    /// <summary>
    /// Farbe des nächsten Tiers als Hex-String.
    /// </summary>
    [ObservableProperty]
    private string _nextTierColor = "#9E9E9E";

    /// <summary>
    /// Fortschritt zum nächsten Tier (0.0 - 1.0).
    /// </summary>
    [ObservableProperty]
    private double _nextTierProgress;

    /// <summary>
    /// Text für den Fortschrittsbalken (z.B. "Lv. 45 / 100").
    /// </summary>
    [ObservableProperty]
    private string _nextTierProgressText = "";

    /// <summary>
    /// Was der nächste Tier zusätzlich bewahrt.
    /// </summary>
    [ObservableProperty]
    private string _nextTierUnlockText = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE KEEP/LOSE ÜBERSICHT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Liste der Dinge die bei Prestige erhalten bleiben (+ Prefix).
    /// </summary>
    [ObservableProperty]
    private string _prestigeKeepList = "";

    /// <summary>
    /// Liste der Dinge die bei Prestige verloren gehen (- Prefix).
    /// </summary>
    [ObservableProperty]
    private string _prestigeLoseList = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE HISTORY
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Letzte Prestige-Durchläufe für die Anzeige (max. 10).
    /// </summary>
    [ObservableProperty]
    private List<PrestigeHistoryDisplay> _prestigeHistory = [];

    /// <summary>
    /// Ob es Prestige-History gibt.
    /// </summary>
    public bool HasPrestigeHistory => PrestigeHistory.Count > 0;

    partial void OnPrestigeHistoryChanged(List<PrestigeHistoryDisplay> value)
        => OnPropertyChanged(nameof(HasPrestigeHistory));

    // ═══════════════════════════════════════════════════════════════════════
    // TIER-ROADMAP (SkiaSharp Visualisierung)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Anzahl Prestiges pro Tier [Bronze..Legende] für die SkiaSharp-Roadmap.
    /// </summary>
    [ObservableProperty]
    private int[] _roadmapTierCounts = new int[7];

    /// <summary>
    /// Höchster jemals erreichter Prestige-Tier (für Roadmap-Unlocks).
    /// </summary>
    [ObservableProperty]
    private PrestigeTier _roadmapCurrentTier = PrestigeTier.None;

    // ═══════════════════════════════════════════════════════════════════════
    // PRESTIGE SHOP
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _prestigePoints;

    [ObservableProperty]
    private ObservableCollection<PrestigeShopItemDisplay> _prestigeShopItems = [];

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public StatisticsViewModel(
        IGameStateService gameStateService,
        IPrestigeService prestigeService,
        IAudioService audioService,
        ILocalizationService localizationService,
        IPurchaseService purchaseService,
        IGameLoopService gameLoopService,
        IDialogService dialogService,
        IPlayGamesService? playGamesService = null)
    {
        _gameStateService = gameStateService;
        _prestigeService = prestigeService;
        _audioService = audioService;
        _localizationService = localizationService;
        _purchaseService = purchaseService;
        _gameLoopService = gameLoopService;
        _dialogService = dialogService;
        _playGamesService = playGamesService;

        LoadStatistics();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    private void LoadStatistics()
    {
        var state = _gameStateService.State;

        // Player stats
        PlayerLevel = state.PlayerLevel;
        TotalXp = state.TotalXp;
        TotalPlayTime = FormatPlayTime(state.Statistics.TotalPlayTimeSeconds);
        TotalMoneyEarned = FormatMoney(state.TotalMoneyEarned);
        TotalMoneySpent = FormatMoney(state.TotalMoneySpent);
        CurrentBalance = FormatMoney(state.Money);

        // Orders & Mini-games
        TotalOrdersCompleted = state.Statistics.TotalOrdersCompleted;
        TotalMiniGamesPlayed = state.Statistics.TotalMiniGamesPlayed;
        PerfectRatings = state.Statistics.PerfectRatings;
        CurrentPerfectStreak = state.Statistics.PerfectStreak;
        BestPerfectStreak = state.Statistics.BestPerfectStreak;
        PerfectRate = state.Statistics.TotalMiniGamesPlayed > 0
            ? (double)state.Statistics.PerfectRatings / state.Statistics.TotalMiniGamesPlayed * 100
            : 0;

        // Prestige (7-Tier System: Bronze bis Legende)
        PrestigeLevel = state.Prestige.TotalPrestigeCount;
        PrestigeMultiplier = $"{_prestigeService.GetPermanentMultiplier():F1}x";
        var highestTier = state.Prestige.GetHighestAvailableTier(state.PlayerLevel);
        CanPrestige = highestTier != PrestigeTier.None;
        MinimumPrestigeLevel = PrestigeTier.Bronze.GetRequiredLevel();
        // Basis-PP mit Tier-Multiplikator anzeigen (nicht nur Basis-PP)
        var basePoints = _prestigeService.GetPrestigePoints(state.CurrentRunMoney);
        var tierMultiplier = highestTier != PrestigeTier.None ? highestTier.GetPointMultiplier() : 1.0m;
        var potentialPoints = (int)(basePoints * tierMultiplier);
        PotentialBonus = $"+{potentialPoints} PP";

        // Fortschritt zum nächsten Tier berechnen
        CalculateNextTierProgress(state, highestTier);

        // Tier-Roadmap Daten (für SkiaSharp-Visualisierung)
        RoadmapTierCounts =
        [
            state.Prestige.BronzeCount,
            state.Prestige.SilverCount,
            state.Prestige.GoldCount,
            state.Prestige.PlatinCount,
            state.Prestige.DiamantCount,
            state.Prestige.MeisterCount,
            state.Prestige.LegendeCount,
        ];
        RoadmapCurrentTier = GetHighestAchievedTier(state.Prestige);

        // Workshops
        TotalWorkshops = state.Workshops.Count;
        TotalWorkers = state.Workshops.Sum(w => w.Workers.Count);
        TotalIncomePerSecond = FormatMoneyPerSecond(state.TotalIncomePerSecond);

        // Play Games Verfügbarkeit prüfen
        IsPlayGamesAvailable = _playGamesService?.IsSignedIn ?? false;

        // Prestige-Pass Status
        IsPrestigePassActive = state.IsPrestigePassActive;
        CanBuyPrestigePass = !state.IsPrestigePassActive;

        WorkshopStats = state.Workshops
            .OrderBy(w => w.Type.GetUnlockLevel())
            .Select(w => new WorkshopStatistic
            {
                Name = GetWorkshopName(w.Type),
                Icon = GetWorkshopIcon(w.Type),
                Level = w.Level,
                Workers = w.Workers.Count,
                IncomePerSecond = FormatMoneyPerSecond(w.IncomePerSecond)
            })
            .ToList();

        // Prestige-Übersicht (Keep/Lose) aufbauen
        BuildPrestigeOverview(highestTier);

        // Prestige-History laden (letzte 10)
        LoadPrestigeHistory(state);

        // Prestige-Shop laden
        RefreshPrestigeShop();
    }

    private void RefreshPrestigeShop()
    {
        var state = _gameStateService.State;
        PrestigePoints = state.Prestige.PrestigePoints;

        var shopItems = _prestigeService.GetShopItems();
        var recommendedId = GetRecommendedItemId(shopItems);
        PrestigeShopItems.Clear();

        // Items nach Kategorie gruppiert einfügen
        var grouped = shopItems
            .GroupBy(i => i.Category)
            .OrderBy(g => (int)g.Key);

        foreach (var group in grouped)
        {
            // Kategorie-Header einfügen
            var (catName, catIcon) = GetCategoryInfo(group.Key);
            PrestigeShopItems.Add(new PrestigeShopItemDisplay
            {
                IsHeader = true,
                CategoryName = catName,
                CategoryIcon = catIcon,
            });

            // Items dieser Kategorie (ungekaufte zuerst, dann günstigste)
            foreach (var item in group.OrderBy(i => i.IsPurchased).ThenBy(i => i.Cost))
            {
                // Wiederholbare Items: Eskalierte Kosten berechnen
                int displayCost = item.IsRepeatable
                    ? PrestigeService.GetRepeatableItemCost(item, item.PurchaseCount)
                    : item.Cost;

                PrestigeShopItems.Add(new PrestigeShopItemDisplay
                {
                    Id = item.Id,
                    Icon = item.Icon,
                    Name = _localizationService.GetString(item.NameKey) ?? item.NameKey,
                    Description = item.IsRepeatable
                        ? $"{_localizationService.GetString(item.DescriptionKey) ?? item.DescriptionKey} (x{item.PurchaseCount})"
                        : _localizationService.GetString(item.DescriptionKey) ?? item.DescriptionKey,
                    Cost = displayCost,
                    IsPurchased = item.IsPurchased,
                    CanAfford = !item.IsPurchased && PrestigePoints >= displayCost,
                    IsRecommended = item.Id == recommendedId,
                    IsRepeatable = item.IsRepeatable,
                    PurchaseCount = item.PurchaseCount,
                });
            }
        }
    }

    /// <summary>
    /// Gibt den lokalisierten Kategorie-Namen und das Icon für eine Shop-Kategorie zurück.
    /// </summary>
    private (string Name, string Icon) GetCategoryInfo(PrestigeShopCategory category) => category switch
    {
        PrestigeShopCategory.IncomeAndCosts => (
            _localizationService.GetString("ShopCatIncome") ?? "Einkommen & Kosten", "Cash"),
        PrestigeShopCategory.WorkerAndMood => (
            _localizationService.GetString("ShopCatWorker") ?? "Arbeiter & Stimmung", "HardHat"),
        PrestigeShopCategory.SpeedAndAutomation => (
            _localizationService.GetString("ShopCatSpeed") ?? "Beschleunigung", "LightningBolt"),
        PrestigeShopCategory.CurrencyAndStart => (
            _localizationService.GetString("ShopCatCurrency") ?? "Währung & Start", "Bank"),
        _ => ("", "")
    };

    [RelayCommand]
    private async Task ShowLeaderboardsAsync()
    {
        if (_playGamesService?.IsSignedIn == true)
            await _playGamesService.ShowLeaderboardsAsync();
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private void RefreshStatistics()
    {
        LoadStatistics();
    }

    [RelayCommand]
    private async Task BuyPrestigePassAsync()
    {
        if (_gameStateService.State.IsPrestigePassActive) return;

        // Echten IAP-Kauf durchführen (2,99 EUR)
        var success = await _purchaseService.PurchaseConsumableAsync("prestige_pass");
        if (!success) return;

        _prestigeService.ActivatePrestigePass();
        await _audioService.PlaySoundAsync(GameSound.LevelUp);

        IsPrestigePassActive = true;
        CanBuyPrestigePass = false;

        _dialogService.ShowAlertDialog(
            _localizationService.GetString("PrestigePassTitle") ?? "Prestige-Pass",
            _localizationService.GetString("PrestigePassActive") ?? "Prestige-Pass aktiviert! +50% Prestige-Punkte beim nächsten Prestige.",
            _localizationService.GetString("Great") ?? "Super!");
    }

    [RelayCommand]
    private async Task BuyPrestigeItemAsync(PrestigeShopItemDisplay? item)
    {
        if (item == null || item.IsPurchased || !item.CanAfford) return;

        var success = _prestigeService.BuyShopItem(item.Id);
        if (success)
        {
            _gameLoopService.InvalidatePrestigeEffects();
            await _audioService.PlaySoundAsync(GameSound.Upgrade);
            RefreshPrestigeShop();
        }
    }

    /// <summary>
    /// Event to show the prestige dialog.
    /// </summary>
    public event EventHandler? ShowPrestigeDialog;

    [RelayCommand]
    private async Task PrestigeAsync()
    {
        var state = _gameStateService.State;
        var highestTier = state.Prestige.GetHighestAvailableTier(state.PlayerLevel);

        if (highestTier == PrestigeTier.None)
        {
            var title = _localizationService.GetString("PrestigeNotAvailable");
            var message = string.Format(
                _localizationService.GetString("PrestigeNotAvailableDesc"),
                PrestigeTier.Bronze.GetRequiredLevel(),
                state.PlayerLevel);

            _dialogService.ShowAlertDialog(title, message, _localizationService.GetString("OK") ?? "OK");
            return;
        }

        await _audioService.PlaySoundAsync(GameSound.ButtonTap);

        // Fire event to show dialog - the page handles the actual dialog
        ShowPrestigeDialog?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Called after prestige is completed to refresh statistics.
    /// </summary>
    public async Task OnPrestigeCompletedAsync()
    {
        await _audioService.PlaySoundAsync(GameSound.LevelUp);
        LoadStatistics();
    }

    /// <summary>
    /// Berechnet den Fortschritt zum nächsten Prestige-Tier.
    /// Zeigt Level-Fortschritt, Tier-Name, Icon, Farbe und was der nächste Tier freischaltet.
    /// </summary>
    private void CalculateNextTierProgress(GameState state, PrestigeTier currentHighest)
    {
        // Nächster Tier ermitteln: Der nächsthöhere Tier über dem aktuell verfügbaren
        var nextTier = currentHighest.GetNextTier();

        // Wenn bereits auf Legende (höchster Tier), kein Fortschritts-Indikator nötig
        if (nextTier == PrestigeTier.None)
        {
            HasNextTier = false;
            return;
        }

        HasNextTier = true;
        var requiredLevel = nextTier.GetRequiredLevel();

        // Fortschritt berechnen: Aktuelles Level relativ zum Ziel-Level
        // Startpunkt ist das Level des aktuellen Tiers (oder 0 wenn keiner)
        var currentTierLevel = currentHighest != PrestigeTier.None
            ? currentHighest.GetRequiredLevel()
            : 0;
        var range = requiredLevel - currentTierLevel;
        var progress = range > 0
            ? Math.Clamp((double)(state.PlayerLevel - currentTierLevel) / range, 0.0, 1.0)
            : 0.0;

        NextTierProgress = progress;
        NextTierProgressText = $"Lv. {state.PlayerLevel} / {requiredLevel}";

        // Tier-Infos
        NextTierName = _localizationService.GetString(nextTier.GetLocalizationKey()) ?? nextTier.ToString();
        NextTierIcon = nextTier.GetIcon();
        NextTierColor = nextTier.GetColorKey();

        // Was dieser Tier neu freischaltet (Unterschied zum Vorgänger)
        var unlocks = new List<string>();
        if (nextTier.KeepsResearch() && !currentHighest.KeepsResearch())
            unlocks.Add(_localizationService.GetString("PrestigeKeepsResearch") ?? "Forschung bleibt");
        if (nextTier.KeepsShopItems() && !currentHighest.KeepsShopItems())
            unlocks.Add(_localizationService.GetString("PrestigeKeepsShop") ?? "Shop bleibt");
        if (nextTier.KeepsMasterTools() && !currentHighest.KeepsMasterTools())
            unlocks.Add(_localizationService.GetString("PrestigeKeepsTools") ?? "Meisterwerkzeuge bleiben");
        if (nextTier.KeepsBuildings() && !currentHighest.KeepsBuildings())
            unlocks.Add(_localizationService.GetString("PrestigeKeepsBuildings") ?? "Gebäude bleiben");
        if (nextTier.KeepsEquipment() && !currentHighest.KeepsEquipment())
            unlocks.Add(_localizationService.GetString("PrestigeKeepsEquipment") ?? "Ausrüstung bleibt");
        if (nextTier.KeepsManagers() && !currentHighest.KeepsManagers())
            unlocks.Add(_localizationService.GetString("PrestigeKeepsManagers") ?? "Manager bleiben");
        if (nextTier.KeepsBestWorkers() && !currentHighest.KeepsBestWorkers())
            unlocks.Add(_localizationService.GetString("PrestigeKeepsWorkers") ?? "Beste Worker bleiben");

        // PP-Multiplikator als Highlight
        unlocks.Insert(0, $"x{nextTier.GetPointMultiplier()} PP");

        NextTierUnlockText = string.Join("  |  ", unlocks);
    }

    /// <summary>
    /// Baut die Keep/Lose-Listen basierend auf dem aktuellen Prestige-Tier auf.
    /// Zeigt dem Spieler transparent was bei einem Prestige erhalten bleibt und was verloren geht.
    /// </summary>
    private void BuildPrestigeOverview(PrestigeTier tier)
    {
        // Basis: Was immer erhalten bleibt
        var keep = new List<string>
        {
            _localizationService.GetString("Achievements") ?? "Achievements",
            _localizationService.GetString("PrestigePointsShort") ?? "Prestige-Punkte",
            _localizationService.GetString("Settings") ?? "Einstellungen",
        };

        // Tier-spezifische Bewahrungen (progressive Bewahrung)
        if (tier.KeepsResearch())
            keep.Add(_localizationService.GetString("Research") ?? "Forschung");
        if (tier.KeepsShopItems())
            keep.Add(_localizationService.GetString("PrestigeShop") ?? "Prestige-Shop");
        if (tier.KeepsMasterTools())
            keep.Add(_localizationService.GetString("MasterTools") ?? "Meisterwerkzeuge");
        if (tier.KeepsBuildings())
            keep.Add(_localizationService.GetString("Buildings") ?? "Gebäude");
        if (tier.KeepsEquipment())
            keep.Add(_localizationService.GetString("Equipment") ?? "Ausrüstung");
        if (tier.KeepsManagers())
            keep.Add(_localizationService.GetString("Managers") ?? "Vorarbeiter");
        if (tier.KeepsBestWorkers())
            keep.Add(_localizationService.GetString("BestWorkers") ?? "Beste Arbeiter");

        // Was verloren geht (inverse der Bewahrung)
        var lose = new List<string>
        {
            _localizationService.GetString("WorkshopsTab") ?? "Werkstätten",
            _localizationService.GetString("Workers") ?? "Arbeiter",
            _localizationService.GetString("Money") ?? "Geld",
        };

        if (!tier.KeepsResearch())
            lose.Add(_localizationService.GetString("Research") ?? "Forschung");
        if (!tier.KeepsMasterTools())
            lose.Add(_localizationService.GetString("MasterTools") ?? "Meisterwerkzeuge");
        if (!tier.KeepsBuildings())
            lose.Add(_localizationService.GetString("Buildings") ?? "Gebäude");
        if (!tier.KeepsEquipment())
            lose.Add(_localizationService.GetString("Equipment") ?? "Ausrüstung");
        if (!tier.KeepsManagers())
            lose.Add(_localizationService.GetString("Managers") ?? "Vorarbeiter");

        PrestigeKeepList = string.Join("\n", keep.Select(k => $"+ {k}"));
        PrestigeLoseList = string.Join("\n", lose.Select(l => $"\u2212 {l}"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TIER-DETAIL POPUP (Tap auf Roadmap-Medaille)
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly PrestigeTier[] AllTiers =
    [
        PrestigeTier.Bronze, PrestigeTier.Silver, PrestigeTier.Gold,
        PrestigeTier.Platin, PrestigeTier.Diamant, PrestigeTier.Meister, PrestigeTier.Legende
    ];

    /// <summary>
    /// Zeigt einen Detail-Dialog für den angetippten Tier der Roadmap.
    /// </summary>
    public void ShowTierDetail(int tierIndex)
    {
        if (tierIndex < 0 || tierIndex >= AllTiers.Length) return;

        var tier = AllTiers[tierIndex];
        var tierName = _localizationService.GetString(tier.GetLocalizationKey()) ?? tier.ToString();
        var lines = new List<string>();

        // Level-Anforderung
        var levelLabel = _localizationService.GetString("RequiredLevel") ?? "Benötigtes Level";
        lines.Add($"{levelLabel}: {tier.GetRequiredLevel()}");

        // Vorgänger-Anforderung
        var reqCount = tier.GetRequiredPreviousTierCount();
        if (reqCount > 0)
        {
            var prevTier = (PrestigeTier)((int)tier - 1);
            var prevName = _localizationService.GetString(prevTier.GetLocalizationKey()) ?? prevTier.ToString();
            var reqLabel = _localizationService.GetString("Required") ?? "Benötigt";
            lines.Add($"{reqLabel}: {reqCount}x {prevName}");
        }

        lines.Add("");

        // Belohnungen
        var rewardLabel = _localizationService.GetString("Rewards") ?? "Belohnungen";
        lines.Add($"{rewardLabel}:");
        lines.Add($"  x{tier.GetPointMultiplier()} PP");
        lines.Add($"  +{tier.GetPermanentMultiplierBonus() * 100:0}% {_localizationService.GetString("PermanentIncomeBonus") ?? "Einkommens-Bonus"}");

        // Was bleibt erhalten (progressive Bewahrung)
        var keeps = new List<string>();
        if (tier.KeepsResearch()) keeps.Add(_localizationService.GetString("Research") ?? "Forschung");
        if (tier.KeepsShopItems()) keeps.Add(_localizationService.GetString("PrestigeShop") ?? "Prestige-Shop");
        if (tier.KeepsMasterTools()) keeps.Add(_localizationService.GetString("MasterTools") ?? "Meisterwerkzeuge");
        if (tier.KeepsBuildings()) keeps.Add(_localizationService.GetString("Buildings") ?? "Gebäude");
        if (tier.KeepsEquipment()) keeps.Add(_localizationService.GetString("Equipment") ?? "Ausrüstung");
        if (tier.KeepsManagers()) keeps.Add(_localizationService.GetString("Managers") ?? "Vorarbeiter");
        if (tier.KeepsBestWorkers()) keeps.Add(_localizationService.GetString("BestWorkers") ?? "Beste Arbeiter");

        if (keeps.Count > 0)
        {
            lines.Add("");
            var keepsLabel = _localizationService.GetString("PrestigeKeeps") ?? "Bewahrt";
            lines.Add($"{keepsLabel}:");
            foreach (var k in keeps)
                lines.Add($"  + {k}");
        }

        // Aktueller Status (wie oft bereits abgeschlossen)
        int count = tierIndex < RoadmapTierCounts.Length ? RoadmapTierCounts[tierIndex] : 0;
        if (count > 0)
        {
            lines.Add("");
            var completedLabel = _localizationService.GetString("Completed") ?? "Abgeschlossen";
            lines.Add($"{completedLabel}: {count}x");
        }

        _dialogService.ShowAlertDialog(tierName, string.Join("\n", lines), _localizationService.GetString("OK") ?? "OK");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EMPFEHLUNGS-SYSTEM (Bester Wert im Prestige-Shop)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ermittelt das beste nächste Item im Prestige-Shop basierend auf Kosten-Effizienz.
    /// Priorisierung: 1) pp_income_10, 2) pp_cost_15, 3) pp_start_money, 4) günstigstes ungekauftes Item.
    /// </summary>
    private static string GetRecommendedItemId(IReadOnlyList<PrestigeShopItem> shopItems)
    {
        // Prioritätenliste: Beste ROI-Items zuerst
        string[] priorityOrder = ["pp_income_10", "pp_cost_15", "pp_start_money", "pp_mood_slow", "pp_xp_15"];

        foreach (var id in priorityOrder)
        {
            var item = shopItems.FirstOrDefault(i => i.Id == id);
            if (item != null && !item.IsPurchased)
                return item.Id;
        }

        // Fallback: günstigstes ungekauftes Item (leer wenn alle gekauft)
        return shopItems
            .Where(i => !i.IsPurchased)
            .OrderBy(i => i.Cost)
            .FirstOrDefault()?.Id ?? string.Empty;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static string FormatPlayTime(long seconds)
    {
        var timeSpan = TimeSpan.FromSeconds(seconds);
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
    }

    private static string FormatMoney(decimal amount) => MoneyFormatter.Format(amount, 2);

    private static string FormatMoneyPerSecond(decimal amount) => MoneyFormatter.FormatPerSecond(amount);

    private string GetWorkshopName(WorkshopType type) =>
        _localizationService.GetString(type.GetLocalizationKey());

    private static string GetWorkshopIcon(WorkshopType type) => type.GetIconKind();

    /// <summary>
    /// Lädt die Prestige-History (letzte 10 Einträge) für die UI-Anzeige.
    /// </summary>
    private void LoadPrestigeHistory(GameState state)
    {
        var entries = state.Prestige.History;
        if (entries.Count == 0)
        {
            PrestigeHistory = [];
            return;
        }

        PrestigeHistory = entries.Take(10).Select(e => new PrestigeHistoryDisplay
        {
            TierName = _localizationService.GetString(e.Tier.GetLocalizationKey()) ?? e.Tier.ToString(),
            TierColor = e.Tier.GetColorKey(),
            TierIcon = e.Tier.GetIcon(),
            PointsText = $"+{e.PointsEarned} PP",
            LevelText = $"Lv. {e.PlayerLevel}",
            DateText = FormatHistoryDate(e.Date),
            MoneyText = FormatMoney(e.TotalMoneyEarned),
            MultiplierText = $"{e.MultiplierAfter:F1}x",
        }).ToList();
    }

    /// <summary>
    /// Formatiert ein Datum relativ: "Heute", "Gestern", oder "vor X Tagen".
    /// </summary>
    private string FormatHistoryDate(DateTime utcDate)
    {
        var now = DateTime.UtcNow;
        var days = (int)(now - utcDate).TotalDays;
        if (days == 0) return _localizationService.GetString("Today") ?? "Heute";
        if (days == 1) return _localizationService.GetString("Yesterday") ?? "Gestern";
        return string.Format(
            _localizationService.GetString("DaysAgo") ?? "vor {0} Tagen",
            days);
    }

    /// <summary>
    /// Ermittelt den höchsten Tier den der Spieler jemals erreicht hat (basierend auf Counts).
    /// </summary>
    private static PrestigeTier GetHighestAchievedTier(PrestigeData prestige)
    {
        if (prestige.LegendeCount > 0) return PrestigeTier.Legende;
        if (prestige.MeisterCount > 0) return PrestigeTier.Meister;
        if (prestige.DiamantCount > 0) return PrestigeTier.Diamant;
        if (prestige.PlatinCount > 0) return PrestigeTier.Platin;
        if (prestige.GoldCount > 0) return PrestigeTier.Gold;
        if (prestige.SilverCount > 0) return PrestigeTier.Silver;
        if (prestige.BronzeCount > 0) return PrestigeTier.Bronze;
        return PrestigeTier.None;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// SUPPORTING TYPES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Represents statistics for a single workshop.
/// </summary>
public class WorkshopStatistic
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public int Level { get; set; }
    public int Workers { get; set; }
    public string IncomePerSecond { get; set; } = "";
}

/// <summary>
/// Display-Modell für Prestige-Shop-Items im UI.
/// Dient auch als Kategorie-Header (IsHeader=true).
/// </summary>
public class PrestigeShopItemDisplay
{
    public string Id { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Cost { get; set; }
    public bool IsPurchased { get; set; }
    public bool CanAfford { get; set; }

    /// <summary>Ob das Item wiederholbar kaufbar ist.</summary>
    public bool IsRepeatable { get; set; }

    /// <summary>Aktuelle Kaufanzahl (nur für wiederholbare Items).</summary>
    public int PurchaseCount { get; set; }

    /// <summary>Ob dieses Element ein Kategorie-Header ist (kein kaufbares Item).</summary>
    public bool IsHeader { get; set; }

    /// <summary>Kategorie-Name für Header-Elemente.</summary>
    public string CategoryName { get; set; } = "";

    /// <summary>Kategorie-Icon für Header-Elemente.</summary>
    public string CategoryIcon { get; set; } = "";

    /// <summary>
    /// Kosten-Anzeige mit PP-Suffix.
    /// </summary>
    public string CostDisplay => $"{Cost} PP";

    /// <summary>
    /// Hintergrundfarbe: Gold-transparent für gekauft, grau für gesperrt.
    /// Wiederholbare Items haben immer den Standard-Hintergrund.
    /// </summary>
    public string IconBackground => IsPurchased ? "#40FFD700" : "#20808080";

    /// <summary>
    /// Opacity: Gekaufte Items leicht gedimmt. Wiederholbare nie gedimmt.
    /// </summary>
    public double DisplayOpacity => IsPurchased && !IsRepeatable ? 0.6 : 1.0;

    /// <summary>
    /// Kosten-Farbe: Grün wenn leistbar, Rot wenn nicht, Grau wenn gekauft.
    /// </summary>
    public string CostColor => IsPurchased ? "#808080" : CanAfford ? "#22C55E" : "#EF4444";

    /// <summary>
    /// Ob dieses Item vom Empfehlungs-System als "Bester Wert" markiert ist.
    /// </summary>
    public bool IsRecommended { get; set; }
}

/// <summary>
/// Display-Modell für einen Prestige-History-Eintrag.
/// </summary>
public class PrestigeHistoryDisplay
{
    /// <summary>Lokalisierter Tier-Name (z.B. "Gold").</summary>
    public string TierName { get; set; } = "";

    /// <summary>Tier-Farbe als Hex-String.</summary>
    public string TierColor { get; set; } = "#9E9E9E";

    /// <summary>Tier-Icon (Material Icon Kind).</summary>
    public string TierIcon { get; set; } = "";

    /// <summary>Erhaltene Punkte (z.B. "+12 PP").</summary>
    public string PointsText { get; set; } = "";

    /// <summary>Spieler-Level (z.B. "Lv. 45").</summary>
    public string LevelText { get; set; } = "";

    /// <summary>Relatives Datum (z.B. "Heute", "vor 3 Tagen").</summary>
    public string DateText { get; set; } = "";

    /// <summary>Verdientes Geld (formatiert).</summary>
    public string MoneyText { get; set; } = "";

    /// <summary>Multiplikator nach Prestige (z.B. "1.3x").</summary>
    public string MultiplierText { get; set; } = "";
}
