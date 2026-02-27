using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models.Collection;
using BomberBlast.Models.Entities;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für das Sammlungs-Album.
/// Zeigt entdeckte Gegner, Bosse, PowerUps, Karten und Kosmetik mit Fortschritt und Meilensteinen.
/// </summary>
public partial class CollectionViewModel : ObservableObject, INavigable, IGameJuiceEmitter
{
    private readonly ICollectionService _collectionService;
    private readonly ILocalizationService _localizationService;
    private readonly IBattlePassService _battlePassService;
    private readonly ILeagueService _leagueService;
    private readonly IAchievementService _achievementService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<NavigationRequest>? NavigationRequested;
    public event Action<string, string>? FloatingTextRequested;
    public event Action? CelebrationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private int _selectedCategoryIndex;

    [ObservableProperty]
    private string _selectedCategoryName = "";

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private string _totalProgressText = "";

    [ObservableProperty]
    private string _emptyItemsText = "";

    [ObservableProperty]
    private int _totalProgressPercent;

    [ObservableProperty]
    private int _categoryProgressPercent;

    [ObservableProperty]
    private CollectionEntry? _selectedEntry;

    [ObservableProperty]
    private string _detailName = "";

    [ObservableProperty]
    private string _detailLore = "";

    [ObservableProperty]
    private string _detailStats = "";

    [ObservableProperty]
    private bool _showDetail;

    [ObservableProperty]
    private bool _hasDetailStats;

    [ObservableProperty]
    private string _detailBadgeColor = "#2196F3";

    [ObservableProperty]
    private string _detailIconName = "";

    // Kategorie-Tab visueller Status (aktiver Tab hervorgehoben)
    [ObservableProperty]
    private bool _isCategory0Active = true;

    [ObservableProperty]
    private bool _isCategory1Active;

    [ObservableProperty]
    private bool _isCategory2Active;

    [ObservableProperty]
    private bool _isCategory3Active;

    [ObservableProperty]
    private bool _isCategory4Active;

    // Kategorie-Namen für Tabs
    [ObservableProperty]
    private string _categoryName0 = "";

    [ObservableProperty]
    private string _categoryName1 = "";

    [ObservableProperty]
    private string _categoryName2 = "";

    [ObservableProperty]
    private string _categoryName3 = "";

    [ObservableProperty]
    private string _categoryName4 = "";

    // ═══════════════════════════════════════════════════════════════════════
    // COLLECTIONS
    // ═══════════════════════════════════════════════════════════════════════

    public ObservableCollection<CollectionDisplayItem> Items { get; } = [];
    public ObservableCollection<MilestoneDisplayItem> Milestones { get; } = [];

    // ═══════════════════════════════════════════════════════════════════════
    // KATEGORIE-MAPPING
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly CollectionCategory[] CategoryByIndex =
    [
        CollectionCategory.Enemies,
        CollectionCategory.Bosses,
        CollectionCategory.PowerUps,
        CollectionCategory.BombCards,
        CollectionCategory.Cosmetics
    ];

    private static readonly string[] CategoryNameKeys =
    [
        "CollectionEnemies",
        "CollectionBosses",
        "CollectionPowerUps",
        "CollectionCards",
        "CollectionCosmetics"
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public CollectionViewModel(ICollectionService collectionService, ILocalizationService localizationService,
        IBattlePassService battlePassService, ILeagueService leagueService, IAchievementService achievementService)
    {
        _collectionService = collectionService;
        _localizationService = localizationService;
        _battlePassService = battlePassService;
        _leagueService = leagueService;
        _achievementService = achievementService;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Aufgerufen von MainViewModel beim Anzeigen der Sammlungs-Seite</summary>
    public void OnAppearing()
    {
        UpdateLocalizedTexts();
        LoadCategory();
        UpdateProgress();
        LoadMilestones();
    }

    /// <summary>Lokalisierte Texte aktualisieren (Sprachwechsel)</summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("CollectionTitle") ?? "Collection";

        // Kategorie-Tab-Namen
        for (int i = 0; i < CategoryNameKeys.Length; i++)
        {
            var name = _localizationService.GetString(CategoryNameKeys[i]) ?? "";
            switch (i)
            {
                case 0: CategoryName0 = name; break;
                case 1: CategoryName1 = name; break;
                case 2: CategoryName2 = name; break;
                case 3: CategoryName3 = name; break;
                case 4: CategoryName4 = name; break;
            }
        }

        SelectedCategoryName = _localizationService.GetString(CategoryNameKeys[SelectedCategoryIndex]) ?? "";
        LoadCategory();
        UpdateProgress();
        LoadMilestones();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // KATEGORIE-LADEN
    // ═══════════════════════════════════════════════════════════════════════

    partial void OnSelectedCategoryIndexChanged(int value)
    {
        // Tab-Highlight aktualisieren
        IsCategory0Active = value == 0;
        IsCategory1Active = value == 1;
        IsCategory2Active = value == 2;
        IsCategory3Active = value == 3;
        IsCategory4Active = value == 4;

        LoadCategory();
    }

    /// <summary>Lädt Items für die aktuell ausgewählte Kategorie</summary>
    private void LoadCategory()
    {
        if (SelectedCategoryIndex < 0 || SelectedCategoryIndex >= CategoryByIndex.Length)
            return;

        var category = CategoryByIndex[SelectedCategoryIndex];
        SelectedCategoryName = _localizationService.GetString(CategoryNameKeys[SelectedCategoryIndex]) ?? "";

        var entries = _collectionService.GetEntries(category);

        Items.Clear();
        foreach (var entry in entries)
        {
            Items.Add(CreateDisplayItem(entry, category));
        }

        UpdateProgress();
    }

    /// <summary>Erstellt ein Anzeige-Item aus einem Sammlungs-Eintrag</summary>
    private CollectionDisplayItem CreateDisplayItem(CollectionEntry entry, CollectionCategory category)
    {
        var statLabel = GetStatLabel(category);

        if (!entry.IsDiscovered)
        {
            return new CollectionDisplayItem
            {
                Id = entry.Id,
                Name = "???",
                IconName = "LockOutline",
                IsDiscovered = false,
                StatText = "",
                StatLabel = "",
                BadgeText = "",
                BadgeColor = GetCategoryColor(category),
                Entry = entry,
                Category = category,
                EnemyType = entry.EnemyType,
                BossType = entry.BossType,
                PowerUpType = entry.PowerUpType,
                BombType = entry.BombType
            };
        }

        var name = _localizationService.GetString(entry.NameKey) ?? entry.Id;
        var statText = GetStatText(entry, category);
        var (badgeText, badgeColor) = GetBadgeInfo(entry, category);

        return new CollectionDisplayItem
        {
            Id = entry.Id,
            Name = name,
            IconName = entry.IconName,
            IsDiscovered = true,
            StatText = statText,
            StatLabel = statLabel,
            BadgeText = badgeText,
            BadgeColor = badgeColor,
            Entry = entry,
            Category = category,
            EnemyType = entry.EnemyType,
            BossType = entry.BossType,
            PowerUpType = entry.PowerUpType,
            BombType = entry.BombType
        };
    }

    /// <summary>Stat-Label je nach Kategorie (z.B. "Besiegt", "Gesammelt")</summary>
    private string GetStatLabel(CollectionCategory category) => category switch
    {
        CollectionCategory.Enemies => _localizationService.GetString("CollectionDefeated") ?? "Defeated",
        CollectionCategory.Bosses => _localizationService.GetString("CollectionDefeated") ?? "Defeated",
        CollectionCategory.PowerUps => _localizationService.GetString("CollectionCollected") ?? "Collected",
        _ => ""
    };

    private string GetStatText(CollectionEntry entry, CollectionCategory category)
    {
        return category switch
        {
            CollectionCategory.Enemies => $"x{entry.TimesDefeated}",
            CollectionCategory.Bosses => $"x{entry.TimesDefeated}",
            CollectionCategory.PowerUps => $"x{entry.TimesCollected}",
            CollectionCategory.BombCards => entry.IsOwned
                ? (_localizationService.GetString("CollectionOwned") ?? "Owned")
                : (_localizationService.GetString("CollectionNotOwned") ?? "-"),
            CollectionCategory.Cosmetics => entry.IsOwned
                ? (_localizationService.GetString("CollectionOwned") ?? "Owned")
                : (_localizationService.GetString("CollectionNotOwned") ?? "-"),
            _ => ""
        };
    }

    private (string Text, string Color) GetBadgeInfo(CollectionEntry entry, CollectionCategory category)
    {
        // Kategorie-spezifische Farben für entdeckte Items
        if (category is CollectionCategory.BombCards or CollectionCategory.Cosmetics)
        {
            if (entry.IsOwned)
                return (_localizationService.GetString("CollectionOwned") ?? "Owned", "#4CAF50");
            return (_localizationService.GetString("CollectionNotOwned") ?? "-", GetCategoryColor(category));
        }

        return ("", GetCategoryColor(category));
    }

    /// <summary>Gibt die kategorie-spezifische Akzentfarbe zurück</summary>
    private static string GetCategoryColor(CollectionCategory category) => category switch
    {
        CollectionCategory.Enemies => "#F44336",   // Rot
        CollectionCategory.Bosses => "#FFD700",    // Gold
        CollectionCategory.PowerUps => "#4CAF50",  // Grün
        CollectionCategory.BombCards => "#2196F3",  // Blau
        CollectionCategory.Cosmetics => "#9C27B0", // Lila
        _ => "#2196F3"
    };

    // ═══════════════════════════════════════════════════════════════════════
    // FORTSCHRITT
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateProgress()
    {
        if (SelectedCategoryIndex < 0 || SelectedCategoryIndex >= CategoryByIndex.Length)
            return;

        var category = CategoryByIndex[SelectedCategoryIndex];
        var discovered = _collectionService.GetDiscoveredCount(category);
        var total = _collectionService.GetTotalCount(category);
        var categoryPercent = _collectionService.GetCategoryProgressPercent(category);

        CategoryProgressPercent = categoryPercent;
        ProgressText = $"{discovered}/{total} ({categoryPercent}%)";

        TotalProgressPercent = _collectionService.GetTotalProgressPercent();
        TotalProgressText = $"{TotalProgressPercent}%";
    }

    private void LoadMilestones()
    {
        var milestones = _collectionService.GetMilestones();
        var claimText = _localizationService.GetString("CollectionClaimNow") ?? "Claim";

        Milestones.Clear();
        foreach (var ms in milestones)
        {
            var rewardParts = new List<string>();
            if (ms.CoinReward > 0)
                rewardParts.Add($"{ms.CoinReward:N0} Coins");
            if (ms.GemReward > 0)
                rewardParts.Add($"{ms.GemReward} Gems");

            // Fortschritt zum Meilenstein berechnen (0-100%)
            var milestoneProgress = ms.PercentRequired > 0
                ? Math.Min(100, TotalProgressPercent * 100 / ms.PercentRequired)
                : 100;

            Milestones.Add(new MilestoneDisplayItem
            {
                PercentRequired = ms.PercentRequired,
                PercentLabel = $"{ms.PercentRequired}%",
                RewardText = string.Join(" + ", rewardParts),
                ClaimText = claimText,
                IsClaimed = ms.IsClaimed,
                IsReached = ms.IsReached,
                CanClaim = ms.IsReached && !ms.IsClaimed,
                IsLocked = !ms.IsReached && !ms.IsClaimed,
                MilestoneProgressPercent = milestoneProgress
            });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void GoBack()
    {
        ShowDetail = false;
        NavigationRequested?.Invoke(new GoBack());
    }

    [RelayCommand]
    private void SelectCategory(string indexStr)
    {
        // XAML CommandParameter="0" übergibt string, nicht int
        if (!int.TryParse(indexStr, out var index)) return;
        if (index >= 0 && index < CategoryByIndex.Length)
        {
            SelectedCategoryIndex = index;
        }
    }

    [RelayCommand]
    private void SelectItem(CollectionDisplayItem? item)
    {
        if (item == null || !item.IsDiscovered)
            return;

        SelectedEntry = item.Entry;
        DetailName = item.Name;
        DetailLore = _localizationService.GetString(item.Entry?.LoreKey ?? "") ?? "";
        DetailStats = BuildDetailStats(item);
        HasDetailStats = !string.IsNullOrEmpty(DetailStats);
        DetailBadgeColor = item.BadgeColor;
        DetailIconName = item.IconName;
        ShowDetail = true;
    }

    [RelayCommand]
    private void CloseDetail()
    {
        ShowDetail = false;
        SelectedEntry = null;
    }

    [RelayCommand]
    private void ClaimMilestone(MilestoneDisplayItem? milestone)
    {
        if (milestone == null || !milestone.CanClaim)
            return;

        if (_collectionService.TryClaimMilestone(milestone.PercentRequired))
        {
            LoadMilestones();
            UpdateProgress();

            // Battle Pass XP + Liga-Punkte je nach Meilenstein
            int bpXp = milestone.PercentRequired switch
            {
                25 => BattlePassXpSources.CollectionMilestone25,
                50 => BattlePassXpSources.CollectionMilestone50,
                75 => BattlePassXpSources.CollectionMilestone75,
                100 => BattlePassXpSources.CollectionMilestone100,
                _ => 100
            };
            _battlePassService.AddXp(bpXp, $"collection_milestone_{milestone.PercentRequired}");
            _leagueService.AddPoints(milestone.PercentRequired / 5); // 5/10/15/20 Punkte

            // Achievement: Sammlungs-Fortschritt prüfen
            _achievementService.OnCollectionProgressUpdated(TotalProgressPercent);

            var rewardParts = new List<string>();
            var ms = CollectionMilestone.All.FirstOrDefault(m => m.PercentRequired == milestone.PercentRequired);
            if (ms != null)
            {
                if (ms.CoinReward > 0) rewardParts.Add($"+{ms.CoinReward:N0} Coins");
                if (ms.GemReward > 0) rewardParts.Add($"+{ms.GemReward} Gems");
            }

            FloatingTextRequested?.Invoke(string.Join(" + ", rewardParts), "gold");
            CelebrationRequested?.Invoke();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPER
    // ═══════════════════════════════════════════════════════════════════════

    private string BuildDetailStats(CollectionDisplayItem item)
    {
        if (item.Entry == null)
            return "";

        var entry = item.Entry;
        var parts = new List<string>();

        if (entry.TimesEncountered > 0)
        {
            var label = _localizationService.GetString("CollectionEncountered") ?? "Encountered";
            parts.Add($"{label}: {entry.TimesEncountered}");
        }

        if (entry.TimesDefeated > 0)
        {
            var label = _localizationService.GetString("CollectionDefeated") ?? "Defeated";
            parts.Add($"{label}: {entry.TimesDefeated}");
        }

        if (entry.TimesCollected > 0)
        {
            var label = _localizationService.GetString("CollectionCollected") ?? "Collected";
            parts.Add($"{label}: {entry.TimesCollected}");
        }

        if (entry.IsOwned)
        {
            parts.Add(_localizationService.GetString("CollectionOwned") ?? "Owned");
        }

        return string.Join("\n", parts);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// DISPLAY MODELS
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Anzeige-Item für die Sammlungs-Liste (Premium-Karten-Design wie DeckView)</summary>
public class CollectionDisplayItem
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string IconName { get; init; } = "LockOutline";
    public bool IsDiscovered { get; init; }
    public string StatText { get; init; } = "";
    public string BadgeText { get; init; } = "";
    public string BadgeColor { get; init; } = "#808080";
    public CollectionEntry? Entry { get; init; }

    /// <summary>Kategorie für SkiaSharp-Rendering</summary>
    public CollectionCategory Category { get; init; }

    /// <summary>Typ-Enums für SkiaSharp-Icons (aus CollectionEntry durchgereicht)</summary>
    public EnemyType? EnemyType { get; init; }
    public BossType? BossType { get; init; }
    public PowerUpType? PowerUpType { get; init; }
    public BombType? BombType { get; init; }

    /// <summary>True wenn SkiaSharp-Icon verwendet werden soll (alles außer Kosmetik)</summary>
    public bool IsSkiaIcon => Category != CollectionCategory.Cosmetics;

    // ── Premium-Karten-Styling (analog DeckView) ──

    /// <summary>Kategorie-Glow-Farbe (heller als BadgeColor)</summary>
    public string GlowColorHex => Category switch
    {
        CollectionCategory.Enemies => "#EF9A9A",
        CollectionCategory.Bosses => "#FFE082",
        CollectionCategory.PowerUps => "#A5D6A7",
        CollectionCategory.BombCards => "#90CAF9",
        CollectionCategory.Cosmetics => "#CE93D8",
        _ => "#B0B0B0"
    };

    /// <summary>Border-Opacity (voll bei entdeckten, gedimmt bei gesperrten)</summary>
    public double BorderOpacity => IsDiscovered ? 1.0 : 0.3;

    /// <summary>Shimmer-Overlay-Opacity nach Kategorie</summary>
    public double ShimmerOpacity => IsDiscovered ? Category switch
    {
        CollectionCategory.Bosses => 0.2,
        CollectionCategory.BombCards => 0.15,
        _ => 0.1
    } : 0.03;

    /// <summary>Name-Opacity (voll bei entdeckten, gedimmt bei "???")</summary>
    public double NameOpacity => IsDiscovered ? 1.0 : 0.5;

    /// <summary>Stat-Label z.B. "Besiegt" / "Gesammelt"</summary>
    public string StatLabel { get; init; } = "";
}

/// <summary>Anzeige-Item für die Meilenstein-Liste</summary>
public class MilestoneDisplayItem
{
    public int PercentRequired { get; init; }
    public string PercentLabel { get; init; } = "";
    public string RewardText { get; init; } = "";
    public string ClaimText { get; init; } = "";
    public bool IsClaimed { get; init; }
    public bool IsReached { get; init; }
    public bool CanClaim { get; init; }
    public bool IsLocked { get; init; }
    /// <summary>Fortschritt zum Erreichen dieses Meilensteins (0-100%)</summary>
    public int MilestoneProgressPercent { get; init; }
}
