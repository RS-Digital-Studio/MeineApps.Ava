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
/// Zeigt 50 Level in 5 Welten mit Stern-basiertem World-Gating.
/// Power-Up Boost Overlay ab Level 20 (Rewarded Ad).
/// </summary>
public partial class LevelSelectViewModel : ObservableObject
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

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<LevelDisplayItem> _levels = [];

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

    private string _pendingBoostType = "";

    /// <summary>Vorheriger Stern-Stand fuer Welt-Freischaltungs-Erkennung</summary>
    private int _previousTotalStars = -1;

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
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAppearing()
    {
        BuildLevelList();
        UpdateProgressInfo();
    }

    private void BuildLevelList()
    {
        Levels.Clear();
        int totalStars = _progressService.GetTotalStars();

        // Neue Welt freigeschaltet erkennen → Confetti
        if (_previousTotalStars >= 0 && totalStars > _previousTotalStars)
        {
            for (int w = 2; w <= 5; w++)
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

        for (int i = 1; i <= _progressService.TotalLevels; i++)
        {
            int world = _progressService.GetWorldForLevel(i);
            int worldStarsRequired = _progressService.GetWorldStarsRequired(i);
            bool isWorldLocked = worldStarsRequired > 0 && totalStars < worldStarsRequired;
            bool isFirstInWorld = ((i - 1) % 10) == 0;

            // Welt-Header einfuegen (vor dem ersten Level jeder Welt)
            if (isFirstInWorld && world > 1)
            {
                var header = new LevelDisplayItem
                {
                    LevelNumber = 0,
                    IsWorldHeader = true,
                    WorldNumber = world,
                    WorldStarsRequired = worldStarsRequired,
                    IsWorldLocked = isWorldLocked,
                    WorldLockText = isWorldLocked
                        ? string.Format(_localizationService.GetString("WorldLocked"), worldStarsRequired)
                        : string.Format(_localizationService.GetString("WorldFormat"), world),
                    DisplayText = ""
                };
                Levels.Add(header);
            }

            bool isUnlocked = !isWorldLocked && _progressService.IsLevelUnlocked(i);
            int stars = _progressService.GetLevelStars(i);
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
                IsWorldLocked = isWorldLocked
            };
            item.SelectCommand = new RelayCommand(() => SelectLevel(item));
            Levels.Add(item);
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

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SelectLevel(LevelDisplayItem? level)
    {
        if (level == null || !level.IsUnlocked || level.IsWorldHeader)
            return;

        // Ab Level 20: Boost-Overlay anbieten (nur fuer Free User)
        if (level.LevelNumber >= 20 && !_purchaseService.IsPremium && _rewardedAdService.IsAvailable)
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
}

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

    // Welt-Header Properties
    public bool IsWorldHeader { get; set; }
    public int WorldNumber { get; set; }
    public int WorldStarsRequired { get; set; }
    public bool IsWorldLocked { get; set; }
    public string WorldLockText { get; set; } = "";

    public IRelayCommand? SelectCommand { get; set; }
    public bool IsLocked => !IsUnlocked;
    public string StarsDisplay => StarsText;

    /// <summary>
    /// Welt-basierte Hintergrundfarbe (5 Welten, 5 Farben)
    /// </summary>
    public Color BackgroundColor
    {
        get
        {
            if (IsWorldLocked) return Color.Parse("#333333");
            if (!IsUnlocked) return Color.Parse("#444444");

            // Welt-Farben: Forest, Industrial, Cavern, Sky, Inferno
            int world = (LevelNumber - 1) / 10; // 0-4
            if (IsCompleted)
            {
                return world switch
                {
                    0 => Color.Parse("#2E7D32"), // Forest - Dunkelgrün
                    1 => Color.Parse("#37474F"), // Industrial - Stahlgrau
                    2 => Color.Parse("#4A148C"), // Cavern - Dunkelviolett
                    3 => Color.Parse("#0277BD"), // Sky - Dunkelcyan
                    4 => Color.Parse("#B71C1C"), // Inferno - Dunkelrot
                    _ => Color.Parse("#2E7D32")
                };
            }
            return world switch
            {
                0 => Color.Parse("#388E3C"), // Forest - Grün
                1 => Color.Parse("#546E7A"), // Industrial - Blaugrau
                2 => Color.Parse("#6A1B9A"), // Cavern - Lila
                3 => Color.Parse("#0288D1"), // Sky - Cyan
                4 => Color.Parse("#C62828"), // Inferno - Rot
                _ => Color.Parse("#1565C0")
            };
        }
    }

    public IBrush TextBrush =>
        !IsUnlocked || IsWorldLocked ? Brushes.Gray : Brushes.White;

    // Welt-Header Anzeige-Properties
    public double WorldHeaderOpacity => IsWorldLocked ? 0.5 : 1.0;

    public Material.Icons.MaterialIconKind WorldIconKind =>
        IsWorldLocked ? Material.Icons.MaterialIconKind.Lock : Material.Icons.MaterialIconKind.Earth;

    public IBrush WorldHeaderBrush =>
        IsWorldLocked ? Brushes.Gray : Brush.Parse("#FFD700");
}

/// <summary>
/// Alias fuer LevelDisplayItem in View DataTemplates
/// </summary>
public class LevelItem : LevelDisplayItem { }
