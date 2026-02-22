using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
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
public partial class LevelSelectViewModel : ObservableObject, IDisposable
{
    private readonly IProgressService _progressService;
    private readonly IPurchaseService _purchaseService;
    private readonly ICoinService _coinService;
    private readonly ILocalizationService _localizationService;
    private readonly IRewardedAdService _rewardedAdService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
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
    private MaterialIconKind _boostPowerUpIcon = MaterialIconKind.Flash;

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
    private static readonly (MaterialIconKind Icon, string Primary, string Dark, string Accent, string NameKey)[] WorldConfigs =
    [
        (MaterialIconKind.PineTree,     "#388E3C", "#1B5E20", "#66BB6A", "WorldForest"),
        (MaterialIconKind.Factory,      "#546E7A", "#263238", "#90A4AE", "WorldIndustrial"),
        (MaterialIconKind.DiamondStone, "#6A1B9A", "#311B92", "#AB47BC", "WorldCavern"),
        (MaterialIconKind.Cloud,        "#0288D1", "#01579B", "#4FC3F7", "WorldSky"),
        (MaterialIconKind.Fire,         "#C62828", "#7F0000", "#EF5350", "WorldInferno"),
        (MaterialIconKind.Pillar,       "#8D6E63", "#4E342E", "#BCAAA4", "WorldRuins"),
        (MaterialIconKind.Waves,        "#0277BD", "#004C8C", "#4FC3F7", "WorldOcean"),
        (MaterialIconKind.Terrain,      "#D84315", "#BF360C", "#FF7043", "WorldVolcano"),
        (MaterialIconKind.WeatherSunny, "#FFD600", "#F9A825", "#FFEE58", "WorldSkyFortress"),
        (MaterialIconKind.Ghost,        "#4A148C", "#1A237E", "#CE93D8", "WorldShadowRealm"),
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public LevelSelectViewModel(
        IProgressService progressService,
        IPurchaseService purchaseService,
        ICoinService coinService,
        ILocalizationService localizationService,
        IRewardedAdService rewardedAdService)
    {
        _progressService = progressService;
        _purchaseService = purchaseService;
        _coinService = coinService;
        _localizationService = localizationService;
        _rewardedAdService = rewardedAdService;

        // Coin-Anzeige bei Balance-Aenderung aktualisieren (z.B. nach Kauf im Shop)
        _coinService.BalanceChanged += OnBalanceChanged;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAppearing()
    {
        BuildWorldGroups();
        UpdateProgressInfo();
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

            var group = new WorldGroup
            {
                WorldNumber = w,
                WorldName = worldTitle,
                WorldLockText = isWorldLocked
                    ? string.Format(_localizationService.GetString("WorldLocked"), starsRequired)
                    : worldTitle,
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
                int stars = _progressService.GetLevelStars(i);
                worldStars += stars;

                bool isUnlocked = !isWorldLocked && _progressService.IsLevelUnlocked(i);
                int bestScore = _progressService.GetLevelBestScore(i);
                bool isCompleted = bestScore > 0;

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

        // Ab Level 20: Boost-Overlay anbieten
        bool showBoost = level.LevelNumber >= 20;
        if (showBoost && (_purchaseService.IsPremium || _rewardedAdService.IsAvailable))
        {
            PendingLevel = level.LevelNumber;
            PickRandomBoost();
            ShowBoostOverlay = true;
            return;
        }

        NavigationRequested?.Invoke($"Game?mode=story&level={level.LevelNumber}");
    }

    private void PickRandomBoost()
    {
        var boosts = new[]
        {
            ("speed", MaterialIconKind.Flash),
            ("fire", MaterialIconKind.Fire),
            ("bombs", MaterialIconKind.Bomb)
        };
        var selected = boosts[Random.Shared.Next(boosts.Length)];
        _pendingBoostType = selected.Item1;
        BoostPowerUpIcon = selected.Item2;

        // Lokalisierte Texte
        BoostTitleText = _localizationService.GetString("PowerUpBoost");
        BoostDescText = _localizationService.GetString("PowerUpBoostDesc");
        BoostDeclineText = _localizationService.GetString("WithoutBoost");
        BoostAcceptText = _purchaseService.IsPremium
            ? _localizationService.GetString("BoostFree") ?? "Boost aktivieren"
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
            NavigationRequested?.Invoke($"Game?mode=story&level={PendingLevel}&boost={_pendingBoostType}");
            return;
        }

        // Free: Rewarded Ad
        var success = await _rewardedAdService.ShowAdAsync("power_up");
        if (success)
        {
            NavigationRequested?.Invoke($"Game?mode=story&level={PendingLevel}&boost={_pendingBoostType}");
        }
        else
        {
            // Ad fehlgeschlagen, normal starten
            NavigationRequested?.Invoke($"Game?mode=story&level={PendingLevel}");
        }
    }

    [RelayCommand]
    private void DeclineBoost()
    {
        ShowBoostOverlay = false;
        NavigationRequested?.Invoke($"Game?mode=story&level={PendingLevel}");
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke("..");
    }

    public void Dispose()
    {
        _coinService.BalanceChanged -= OnBalanceChanged;
    }
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
    public bool IsLocked { get; set; }
    public int StarsRequired { get; set; }
    public int StarsEarned { get; set; }
    public int MaxStars { get; set; } = 30;

    // Welt-Farben
    public Color PrimaryColor { get; set; }
    public Color DarkColor { get; set; }
    public Color AccentColor { get; set; }

    // Material Icon
    public MaterialIconKind WorldIcon { get; set; }

    // Level-Items (10 pro Welt)
    public ObservableCollection<LevelDisplayItem> Levels { get; set; } = [];

    // Abgeleitete Properties fuer die View
    public double SectionOpacity => IsLocked ? 0.4 : 1.0;
    public IBrush HeaderTextBrush => IsLocked ? Brushes.Gray : new SolidColorBrush(AccentColor);
    public IBrush HeaderBackgroundBrush => new SolidColorBrush(DarkColor);
    public MaterialIconKind LockIcon => IsLocked ? MaterialIconKind.Lock : WorldIcon;
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

    public IBrush TextBrush =>
        !IsUnlocked || IsWorldLocked ? Brushes.Gray : Brushes.White;
}
