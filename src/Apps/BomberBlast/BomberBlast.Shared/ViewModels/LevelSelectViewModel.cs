using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeineApps.Core.Ava.ViewModels;
using BomberBlast.Icons;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Premium.Ava.Services;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel fuer die Level-Auswahl.
/// Zeigt 100 Level in 10 Welten mit Stern-basiertem World-Gating.
/// Power-Up Boost Overlay ab Level 20 (Rewarded Ad).
/// Implementiert IDisposable fuer BalanceChanged-Unsubscription.
/// </summary>
public sealed partial class LevelSelectViewModel : ViewModelBase, INavigable, IGameJuiceEmitter, IDisposable
{
    private readonly IProgressService _progressService;
    private readonly IPurchaseService _purchaseService;
    private readonly ICoinService _coinService;
    private readonly ILocalizationService _localizationService;
    private readonly IRewardedAdService _rewardedAdService;
    private readonly IMasterModeService _masterModeService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action? CelebrationRequested;
    public event Action<string, string>? FloatingTextRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<WorldGroup> _worldGroups = [];

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private string _starsText = "";

    [ObservableProperty]
    private string _coinsText = "";

    // Power-Up Boost Overlay
    [ObservableProperty]
    private bool _showBoostOverlay;

    [ObservableProperty]
    private string _boostPowerUpName = "";

    [ObservableProperty]
    private GameIconKind _boostPowerUpIcon = GameIconKind.Flash;

    [ObservableProperty]
    private int _pendingLevel;

    [ObservableProperty]
    private string _boostTitleText = "";

    [ObservableProperty]
    private string _boostDescText = "";

    [ObservableProperty]
    private string _boostDeclineText = "";

    [ObservableProperty]
    private string _boostAcceptText = "";

    private string _pendingBoostType = "";

    /// <summary>Vorheriger Stern-Stand fuer Welt-Freischaltungs-Erkennung</summary>
    private int _previousTotalStars = -1;

    // ═══════════════════════════════════════════════════════════════════════
    // WELT-KONFIGURATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Statische Welt-Konfiguration: Icon, Farben, RESX-Key</summary>
    private static readonly (GameIconKind Icon, string Primary, string Dark, string Accent, string NameKey)[] WorldConfigs =
    [
        (GameIconKind.PineTree,     "#388E3C", "#1B5E20", "#66BB6A", "WorldForest"),
        (GameIconKind.Factory,      "#546E7A", "#263238", "#90A4AE", "WorldIndustrial"),
        (GameIconKind.DiamondStone, "#6A1B9A", "#311B92", "#AB47BC", "WorldCavern"),
        (GameIconKind.Cloud,        "#0288D1", "#01579B", "#4FC3F7", "WorldSky"),
        (GameIconKind.Fire,         "#C62828", "#7F0000", "#EF5350", "WorldInferno"),
        (GameIconKind.Pillar,       "#8D6E63", "#4E342E", "#BCAAA4", "WorldRuins"),
        (GameIconKind.Waves,        "#0277BD", "#004C8C", "#4FC3F7", "WorldOcean"),
        (GameIconKind.Terrain,      "#D84315", "#BF360C", "#FF7043", "WorldVolcano"),
        (GameIconKind.WeatherSunny, "#FFD600", "#F9A825", "#FFEE58", "WorldSkyFortress"),
        (GameIconKind.Ghost,        "#4A148C", "#1A237E", "#CE93D8", "WorldShadowRealm"),
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    private readonly ILoadoutService _loadoutService;
    private readonly IGemService _gemService;

    public LevelSelectViewModel(
        IProgressService progressService,
        IPurchaseService purchaseService,
        ICoinService coinService,
        ILocalizationService localizationService,
        IRewardedAdService rewardedAdService,
        IMasterModeService masterModeService,
        ILoadoutService loadoutService,
        IGemService gemService)
    {
        _progressService = progressService;
        _purchaseService = purchaseService;
        _coinService = coinService;
        _localizationService = localizationService;
        _rewardedAdService = rewardedAdService;
        _masterModeService = masterModeService;
        _loadoutService = loadoutService;
        _gemService = gemService;

        // Coin-Anzeige bei Balance-Aenderung aktualisieren (z.B. nach Kauf im Shop)
        _coinService.BalanceChanged += OnBalanceChanged;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MASTER MODE (v2.0.35)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Ob Master Mode freigeschaltet ist (L100 Normal-Modus abgeschlossen).</summary>
    public bool IsMasterModeUnlocked => _masterModeService.IsUnlocked;

    /// <summary>Ob aktuell der Master-Mode-Toggle aktiviert ist (persistiert in Preferences).</summary>
    [ObservableProperty]
    private bool _isMasterModeActive;

    /// <summary>Text für den Master-Mode-Header-Indikator (zeigt Gesamt-Stern-Stand im Master).</summary>
    [ObservableProperty]
    private string _masterModeStatusText = "";

    [RelayCommand]
    private void ToggleMasterMode()
    {
        if (!IsMasterModeUnlocked) return;
        // Service-State ZUERST setzen (Source of Truth), dann VM-Property nachziehen.
        // Verhindert transienten Inkonsistenz-State falls Listener auf PropertyChanged
        // während des Toggle den Service abfragen.
        _masterModeService.IsActive = !_masterModeService.IsActive;
        IsMasterModeActive = _masterModeService.IsActive;
        // Level-Liste neu laden damit Thumbnails Master-Sterne zeigen
        OnAppearing();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAppearing()
    {
        // Master-Mode State aus Service lesen (kann zwischen Sessions persistieren)
        IsMasterModeActive = _masterModeService.IsActive;
        OnPropertyChanged(nameof(IsMasterModeUnlocked));
        UpdateMasterModeStatus();

        BuildWorldGroups();
        UpdateProgressInfo();
    }

    private void UpdateMasterModeStatus()
    {
        if (!IsMasterModeUnlocked)
        {
            MasterModeStatusText = "";
            return;
        }
        var format = _localizationService.GetString("MasterStatusFormat") ?? "Master: {0}/100 ({1}★)";
        MasterModeStatusText = string.Format(format,
            _masterModeService.TotalMasterClears,
            _masterModeService.TotalMaster3Stars);
    }

    /// <summary>
    /// Findet das niedrigste freigeschaltete Level vor der gesperrten Welt, das noch
    /// verbesserbar ist (weniger als 3 Sterne). Liefert 0 wenn alle Levels bereits
    /// 3-Sterne haben (Worst-Case bei sehr fortgeschrittenen Spielern, die trotzdem
    /// kurz vor dem nächsten Welt-Gate stehen — sollte selten sein).
    /// </summary>
    private int FindRecommendedReplayLevel(int firstLockedLevel)
    {
        for (int lvl = 1; lvl < firstLockedLevel; lvl++)
        {
            if (!_progressService.IsLevelUnlocked(lvl)) continue;
            int stars = _progressService.GetLevelStars(lvl);
            if (stars < 3) return lvl;
        }
        return 0;
    }

    private void BuildWorldGroups()
    {
        WorldGroups.Clear();
        int totalStars = _progressService.GetTotalStars();

        // Neue Welt freigeschaltet erkennen → Confetti
        if (_previousTotalStars >= 0 && totalStars > _previousTotalStars)
        {
            for (int w = 2; w <= 10; w++)
            {
                int firstLevelOfWorld = ((w - 1) * 10) + 1;
                int required = _progressService.GetWorldStarsRequired(firstLevelOfWorld);
                if (required > 0 && _previousTotalStars < required && totalStars >= required)
                {
                    CelebrationRequested?.Invoke();
                    break;
                }
            }
        }
        _previousTotalStars = totalStars;

        for (int w = 1; w <= 10; w++)
        {
            int firstLevel = (w - 1) * 10 + 1;
            int starsRequired = _progressService.GetWorldStarsRequired(firstLevel);
            bool isWorldLocked = starsRequired > 0 && totalStars < starsRequired;
            var config = WorldConfigs[w - 1];

            // Lokalisierter Welt-Name
            string worldName = _localizationService.GetString(config.NameKey);
            string worldTitle = $"{string.Format(_localizationService.GetString("WorldFormat"), w)} - {worldName}";

            // Differenz statt absoluter Schwellwert: motiviert mit Spieler-Fortschritt mit
            // ("Noch 25 ★" statt "155 ★ benötigt"). Differenz nie unter 1 (kann nicht 0 sein,
            // da bei isWorldLocked=true totalStars < starsRequired).
            int starsMissing = isWorldLocked ? Math.Max(1, starsRequired - totalStars) : 0;

            // Empfehlung: niedrigstes verbesserbares Level (< 3 Sterne) vor dieser Welt.
            // Hilft dem Spieler konkret zu wissen wo Sterne zu holen sind.
            int recommendedLevel = isWorldLocked ? FindRecommendedReplayLevel(firstLevel) : 0;
            string lockHint = recommendedLevel > 0
                ? string.Format(_localizationService.GetString("WorldLockHint") ?? "Tip: Replay level {0}", recommendedLevel)
                : "";

            var group = new WorldGroup
            {
                WorldNumber = w,
                WorldName = worldTitle,
                WorldLockText = isWorldLocked
                    ? string.Format(_localizationService.GetString("WorldLocked"), starsMissing)
                    : worldTitle,
                WorldLockHint = lockHint,
                IsLocked = isWorldLocked,
                StarsRequired = starsRequired,
                MaxStars = 30,
                PrimaryColor = Color.Parse(config.Primary),
                DarkColor = Color.Parse(config.Dark),
                AccentColor = Color.Parse(config.Accent),
                WorldIcon = config.Icon,
            };

            // Sterne pro Welt zaehlen + Level-Items erstellen
            int worldStars = 0;
            for (int i = firstLevel; i < firstLevel + 10 && i <= _progressService.TotalLevels; i++)
            {
                // Im Master-Modus: Master-Sterne anzeigen. Unlocked bleibt an Normal-Progress
                // gekoppelt (Master ist nur sinnvoll wenn Level normal freigeschaltet).
                int stars = IsMasterModeActive
                    ? _masterModeService.GetMasterStars(i)
                    : _progressService.GetLevelStars(i);
                worldStars += stars;

                bool isUnlocked = !isWorldLocked && _progressService.IsLevelUnlocked(i);
                int bestScore = _progressService.GetLevelBestScore(i);
                bool isCompleted = IsMasterModeActive
                    ? _masterModeService.GetMasterStars(i) > 0
                    : bestScore > 0;

                var item = new LevelDisplayItem
                {
                    LevelNumber = i,
                    DisplayText = i.ToString(),
                    IsUnlocked = isUnlocked,
                    IsCompleted = isCompleted,
                    Stars = stars,
                    StarsText = isCompleted && stars > 0
                        ? new string('\u2605', stars) + new string('\u2606', 3 - stars)
                        : "",
                    BestScore = bestScore,
                    BestScoreText = bestScore > 0 ? bestScore.ToString("N0") : "",
                    IsWorldLocked = isWorldLocked,
                    WorldNumber = w,
                    IsMasterMode = IsMasterModeActive,
                    MasterStars = _masterModeService.GetMasterStars(i),
                };
                item.SelectCommand = new RelayCommand(() => SelectLevel(item));
                group.Levels.Add(item);
            }
            group.StarsEarned = worldStars;

            WorldGroups.Add(group);
        }
    }

    private void UpdateProgressInfo()
    {
        int completed = _progressService.HighestCompletedLevel;
        int total = _progressService.TotalLevels;
        int stars = _progressService.GetTotalStars();
        int maxStars = total * 3;

        ProgressText = $"{completed}/{total}";
        StarsText = $"\u2605 {stars}/{maxStars}";
        CoinsText = _coinService.Balance.ToString("N0");
    }

    /// <summary>
    /// Reagiert auf Coin-Balance-Aenderungen (z.B. nach Shop-Kauf oder Rewarded Ad)
    /// </summary>
    private void OnBalanceChanged(object? sender, EventArgs e)
    {
        CoinsText = _coinService.Balance.ToString("N0");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SelectLevel(LevelDisplayItem? level)
    {
        if (level == null) return;

        // Gesperrtes Level → Feedback an User
        if (!level.IsUnlocked)
        {
            if (level.IsWorldLocked)
            {
                int starsRequired = _progressService.GetWorldStarsRequired(level.LevelNumber);
                var msg = string.Format(
                    _localizationService.GetString("WorldLocked"),
                    starsRequired);
                FloatingTextRequested?.Invoke(msg, "error");
            }
            else
            {
                var msg = string.Format(
                    _localizationService.GetString("LevelLocked") ?? "Complete Level {0} first!",
                    level.LevelNumber - 1);
                FloatingTextRequested?.Invoke(msg, "error");
            }
            return;
        }

        // Ab Level 5: Loadout-Modal anbieten (Coin/Gem-basiert, alternative zum Ad-Boost).
        // Ab Level 20: zusaetzlich Ad-Boost-Overlay verfuegbar.
        // Boost-Overlay bietet "Loadout"-Button als Alternative zum Ad — beide Wege fuehren zu PendingLevel.
        bool adAvailable = _rewardedAdService.IsAvailable && RewardedAdCooldownTracker.CanShowAd;
        bool showAdBoost = level.LevelNumber >= 20 && (_purchaseService.IsPremium || adAvailable);
        bool offerLoadout = level.LevelNumber >= 5;

        if (showAdBoost)
        {
            PendingLevel = level.LevelNumber;
            PickRandomBoost();
            ShowBoostOverlay = true;
            return;
        }
        if (offerLoadout)
        {
            // Direktes Loadout-Modal ohne Ad-Boost-Overlay
            PendingLevel = level.LevelNumber;
            OpenLoadout();
            return;
        }

        NavigationRequested?.Invoke(new GoGame(Mode: "story", Level: level.LevelNumber, MasterMode: IsMasterModeActive));
    }

    private void PickRandomBoost()
    {
        var boosts = new[]
        {
            ("speed", GameIconKind.Flash),
            ("fire", GameIconKind.Fire),
            ("bombs", GameIconKind.Bomb)
        };
        var selected = boosts[Random.Shared.Next(boosts.Length)];
        _pendingBoostType = selected.Item1;
        BoostPowerUpIcon = selected.Item2;

        // Lokalisierte Texte
        BoostTitleText = _localizationService.GetString("PowerUpBoost");
        BoostDescText = _localizationService.GetString("PowerUpBoostDesc");
        BoostDeclineText = _localizationService.GetString("WithoutBoost");
        BoostAcceptText = _purchaseService.IsPremium
            ? _localizationService.GetString("BoostFree") ?? "Activate boost"
            : _localizationService.GetString("WatchVideo");

        BoostPowerUpName = _pendingBoostType switch
        {
            "speed" => _localizationService.GetString("BoostSpeed"),
            "fire" => _localizationService.GetString("BoostFire"),
            "bombs" => _localizationService.GetString("BoostBomb"),
            _ => ""
        };
    }

    [RelayCommand]
    private async Task AcceptBoostAsync()
    {
        ShowBoostOverlay = false;

        // Premium: Boost kostenlos (kein Ad nötig)
        if (_purchaseService.IsPremium)
        {
            NavigationRequested?.Invoke(new GoGame(Mode: "story", Level: PendingLevel, Boost: _pendingBoostType, MasterMode: IsMasterModeActive));
            return;
        }

        // Free: Rewarded Ad
        var success = await _rewardedAdService.ShowAdAsync("power_up");
        if (success)
        {
            RewardedAdCooldownTracker.RecordAdShown();
            NavigationRequested?.Invoke(new GoGame(Mode: "story", Level: PendingLevel, Boost: _pendingBoostType, MasterMode: IsMasterModeActive));
        }
        else
        {
            // Ad fehlgeschlagen, normal starten
            NavigationRequested?.Invoke(new GoGame(Mode: "story", Level: PendingLevel, MasterMode: IsMasterModeActive));
        }
    }

    [RelayCommand]
    private void DeclineBoost()
    {
        ShowBoostOverlay = false;
        NavigationRequested?.Invoke(new GoGame(Mode: "story", Level: PendingLevel, MasterMode: IsMasterModeActive));
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke(new GoBack());
    }

    public void Dispose()
    {
        _coinService.BalanceChanged -= OnBalanceChanged;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LOADOUT (v2.0.42, Plan Task 3.2): Pre-Run-Boost-Auswahl-Modal
    // ═══════════════════════════════════════════════════════════════════════
    // Spieler tappt auf Level → SelectLevel zeigt vorhandenes Boost-Modal (Ad-basiert)
    // ODER kann das neue Loadout-Modal oeffnen ueber den OpenLoadoutCommand-Button im LevelSelect.
    // Loadout-Modal: 5 Toggles (max 2 aktiv), Coin/Gem-Toggle, "Bezahlen + Spielen"-Button.

    [ObservableProperty] private bool _isLoadoutOverlayVisible;
    [ObservableProperty] private bool _useGemsForLoadout;

    /// <summary>5 Boost-Items mit lokalisierten Namen + Coin/Gem-Cost + Selected-State.</summary>
    public ObservableCollection<LoadoutDisplayItem> LoadoutItems { get; } = new();

    /// <summary>Anzahl ausgewaehlter Boosts (max 2).</summary>
    [ObservableProperty] private int _selectedBoostCount;

    /// <summary>Aufsummierte Kosten fuer alle ausgewaehlten Boosts (Coins ODER Gems je nach Toggle).</summary>
    [ObservableProperty] private string _loadoutTotalCostText = "";

    /// <summary>True wenn Spieler genug Coins/Gems hat fuer alle ausgewaehlten Boosts.</summary>
    [ObservableProperty] private bool _canAffordLoadout;

    /// <summary>Lokalisierter Titel des Loadout-Modals.</summary>
    [ObservableProperty] private string _loadoutTitleText = "";

    /// <summary>
    /// Oeffnet das Loadout-Auswahl-Modal fuer das aktuell pendigte Level.
    /// Wenn schon ein Loadout fuer das Level gespeichert ist, sind die Toggles vorausgewaehlt.
    /// </summary>
    [RelayCommand]
    private void OpenLoadout()
    {
        if (PendingLevel <= 0) return;

        // Falls Boost-Overlay aktiv ist, schliessen
        ShowBoostOverlay = false;

        LoadoutTitleText = string.Format(
            _localizationService.GetString("LoadoutTitle") ?? "Pre-Run Boosts",
            PendingLevel);
        UseGemsForLoadout = false;

        var saved = _loadoutService.GetSavedLoadout(PendingLevel);
        var savedTypes = saved.Select(b => b.Type).ToHashSet();

        LoadoutItems.Clear();
        foreach (LoadoutBoostType type in Enum.GetValues<LoadoutBoostType>())
        {
            LoadoutItems.Add(new LoadoutDisplayItem
            {
                Type = type,
                Name = GetLoadoutBoostName(type),
                CoinCost = _loadoutService.GetCoinCost(type),
                GemCost = _loadoutService.GetGemCost(type),
                IsSelected = savedTypes.Contains(type),
            });
        }
        UpdateLoadoutTotals();
        IsLoadoutOverlayVisible = true;
    }

    /// <summary>Toggle ein Boost-Item. Erzwingt Max=2 Auswahl (aelteste wird abgewaehlt).</summary>
    [RelayCommand]
    private void ToggleLoadoutItem(LoadoutDisplayItem? item)
    {
        if (item == null) return;
        if (!item.IsSelected)
        {
            // Zaehlung der bereits selektierten — wenn schon 2, abweisen
            int currentCount = LoadoutItems.Count(i => i.IsSelected);
            if (currentCount >= 2) return;
        }
        item.IsSelected = !item.IsSelected;
        UpdateLoadoutTotals();
    }

    /// <summary>Toggle zwischen Coin- und Gem-Bezahlung.</summary>
    [RelayCommand]
    private void ToggleLoadoutCurrency()
    {
        UseGemsForLoadout = !UseGemsForLoadout;
        UpdateLoadoutTotals();
    }

    /// <summary>Bezahlen + Loadout-Modal schliessen + Level starten.</summary>
    [RelayCommand]
    private void ConfirmLoadout()
    {
        var selected = LoadoutItems.Where(i => i.IsSelected).Select(i => i.Type).ToList();
        if (selected.Count == 0)
        {
            // Kein Boost gewaehlt → einfach starten ohne Loadout
            _loadoutService.ClearLoadout(PendingLevel);
            IsLoadoutOverlayVisible = false;
            NavigationRequested?.Invoke(new GoGame(Mode: "story", Level: PendingLevel, MasterMode: IsMasterModeActive));
            return;
        }

        var result = _loadoutService.Purchase(PendingLevel, selected, UseGemsForLoadout);
        if (result == null)
        {
            FloatingTextRequested?.Invoke(
                _localizationService.GetString("PurchaseFailed") ?? "Not enough currency",
                "error");
            return;
        }

        IsLoadoutOverlayVisible = false;
        NavigationRequested?.Invoke(new GoGame(Mode: "story", Level: PendingLevel, MasterMode: IsMasterModeActive));
    }

    /// <summary>Modal abbrechen — kein Boost angewandt, kein Spielen.</summary>
    [RelayCommand]
    private void CancelLoadout()
    {
        IsLoadoutOverlayVisible = false;
    }

    private void UpdateLoadoutTotals()
    {
        int totalCoins = 0, totalGems = 0;
        int count = 0;
        foreach (var item in LoadoutItems)
        {
            // DisplayCost je nach Currency-Toggle aktualisieren — Item-Level statt komplexes DataTemplate-Binding
            item.DisplayCost = UseGemsForLoadout ? $"{item.GemCost} Gems" : $"{item.CoinCost:N0} Coins";

            if (!item.IsSelected) continue;
            count++;
            totalCoins += item.CoinCost;
            totalGems += item.GemCost;
        }
        SelectedBoostCount = count;
        LoadoutTotalCostText = UseGemsForLoadout
            ? $"{totalGems} Gems"
            : $"{totalCoins:N0} Coins";
        CanAffordLoadout = count == 0
            || (UseGemsForLoadout ? _gemService.CanAfford(totalGems) : _coinService.CanAfford(totalCoins));
    }

    private string GetLoadoutBoostName(LoadoutBoostType type) => type switch
    {
        LoadoutBoostType.ExtraBomb => _localizationService.GetString("LoadoutBoostExtraBomb") ?? "+1 Bomb",
        LoadoutBoostType.ExtraFire => _localizationService.GetString("LoadoutBoostExtraFire") ?? "+1 Range",
        LoadoutBoostType.SpeedBoost => _localizationService.GetString("LoadoutBoostSpeed") ?? "Max Speed",
        LoadoutBoostType.Wallpass => _localizationService.GetString("LoadoutBoostWallpass") ?? "Wallpass",
        LoadoutBoostType.Invincibility => _localizationService.GetString("LoadoutBoostInvincibility") ?? "30s Shield",
        _ => type.ToString()
    };
}

/// <summary>
/// View-Modell-Item fuer das Loadout-Auswahl-Modal (v2.0.42).
/// DisplayCost wird vom ViewModel beim Toggle der Currency neu gesetzt — vermeidet komplexe
/// $parent-Bindings im DataTemplate die Avalonia-Compiled-Bindings nicht unterstuetzen.
/// </summary>
public partial class LoadoutDisplayItem : ObservableObject
{
    [ObservableProperty] private LoadoutBoostType _type;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private int _coinCost;
    [ObservableProperty] private int _gemCost;
    [ObservableProperty] private bool _isSelected;

    /// <summary>Vom ViewModel gesetzter formatierter Cost-Text (Coins oder Gems je nach Toggle).</summary>
    [ObservableProperty] private string _displayCost = "";
}

// ═══════════════════════════════════════════════════════════════════════════
// WORLD GROUP
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Gruppiert eine Welt mit ihren 10 Levels fuer die Level-Auswahl.
/// Jede WorldGroup ist eine visuelle Sektion mit Header und Level-Grid.
/// </summary>
public class WorldGroup
{
    public int WorldNumber { get; set; }
    public string WorldName { get; set; } = "";
    public string WorldLockText { get; set; } = "";

    /// <summary>
    /// Detaillierter Hinweis fuer gesperrte Welten — z.B. "Tipp: Wiederhole Level 7".
    /// Leerer String wenn keine Empfehlung vorliegt (z.B. alle Levels schon 3-Sterne).
    /// </summary>
    public string WorldLockHint { get; set; } = "";

    public bool IsLocked { get; set; }
    public int StarsRequired { get; set; }
    public int StarsEarned { get; set; }
    public int MaxStars { get; set; } = 30;

    /// <summary>True wenn ein Hint vorliegt — fuer XAML-IsVisible-Binding im Lock-Overlay.</summary>
    public bool HasLockHint => !string.IsNullOrEmpty(WorldLockHint);

    // Welt-Farben
    public Color PrimaryColor { get; set; }
    public Color DarkColor { get; set; }
    public Color AccentColor { get; set; }

    // Material Icon
    public GameIconKind WorldIcon { get; set; }

    // Level-Items (10 pro Welt)
    public ObservableCollection<LevelDisplayItem> Levels { get; set; } = [];

    // Abgeleitete Properties fuer die View (Color statt Brush — MVVM-konform, View konvertiert)
    public double SectionOpacity => IsLocked ? 0.4 : 1.0;
    public Color HeaderTextColor => IsLocked ? Colors.Gray : AccentColor;
    public Color HeaderBackgroundColor => DarkColor;
    public GameIconKind LockIcon => IsLocked ? GameIconKind.Lock : WorldIcon;
    public string ProgressText => $"\u2605 {StarsEarned}/{MaxStars}";
}

// ═══════════════════════════════════════════════════════════════════════════
// LEVEL DISPLAY ITEM
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Anzeige-Model fuer ein Level in der Level-Auswahl
/// </summary>
public class LevelDisplayItem
{
    public int LevelNumber { get; set; }
    public string DisplayText { get; set; } = "";
    public bool IsUnlocked { get; set; }
    public bool IsCompleted { get; set; }
    public int Stars { get; set; }
    public string StarsText { get; set; } = "";
    public int BestScore { get; set; }
    public int WorldNumber { get; set; }
    public bool IsWorldLocked { get; set; }

    /// <summary>v2.0.35: Ob dieses Thumbnail im Master-Modus gerendert wird (Krone-Badge).</summary>
    public bool IsMasterMode { get; set; }

    /// <summary>v2.0.35: Anzahl Master-Sterne (0-3), unabhängig vom Master-Toggle.</summary>
    public int MasterStars { get; set; }

    /// <summary>Visibility-Flag für Master-Crown-Badge (nur anzeigen wenn Level Master-Clear hat).</summary>
    public bool HasMasterClear => MasterStars > 0;

    public string BestScoreText { get; set; } = "";

    public IRelayCommand? SelectCommand { get; set; }
    public bool IsLocked => !IsUnlocked;
    public string StarsDisplay => StarsText;

    /// <summary>
    /// Welt-basierte Hintergrundfarbe (10 Welten, 10 Farben)
    /// </summary>
    public Color BackgroundColor
    {
        get
        {
            if (IsWorldLocked) return Color.Parse("#333333");
            if (!IsUnlocked) return Color.Parse("#444444");

            // Welt-Farben: Forest, Industrial, Cavern, Sky, Inferno, Ruinen, Ozean, Vulkan, Himmelsfestung, Schattenwelt
            int world = (LevelNumber - 1) / 10; // 0-9
            if (IsCompleted)
            {
                return world switch
                {
                    0 => Color.Parse("#2E7D32"),  // Forest
                    1 => Color.Parse("#37474F"),  // Industrial
                    2 => Color.Parse("#4A148C"),  // Cavern
                    3 => Color.Parse("#0277BD"),  // Sky
                    4 => Color.Parse("#B71C1C"),  // Inferno
                    5 => Color.Parse("#5D4037"),  // Ruinen
                    6 => Color.Parse("#01579B"),  // Ozean
                    7 => Color.Parse("#BF360C"),  // Vulkan
                    8 => Color.Parse("#F57F17"),  // Himmelsfestung
                    9 => Color.Parse("#311B92"),  // Schattenwelt
                    _ => Color.Parse("#2E7D32")
                };
            }
            return world switch
            {
                0 => Color.Parse("#388E3C"),
                1 => Color.Parse("#546E7A"),
                2 => Color.Parse("#6A1B9A"),
                3 => Color.Parse("#0288D1"),
                4 => Color.Parse("#C62828"),
                5 => Color.Parse("#8D6E63"),  // Ruinen
                6 => Color.Parse("#0277BD"),  // Ozean
                7 => Color.Parse("#D84315"),  // Vulkan
                8 => Color.Parse("#FFD600"),  // Himmelsfestung
                9 => Color.Parse("#4A148C"),  // Schattenwelt
                _ => Color.Parse("#1565C0")
            };
        }
    }

    public Color TextColor =>
        !IsUnlocked || IsWorldLocked ? Colors.Gray : Colors.White;
}
