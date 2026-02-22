using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BomberBlast.Models.Collection;
using BomberBlast.Services;
using MeineApps.Core.Ava.Localization;

namespace BomberBlast.ViewModels;

/// <summary>
/// ViewModel für das Sammlungs-Album.
/// Zeigt entdeckte Gegner, Bosse, PowerUps, Karten und Kosmetik mit Fortschritt und Meilensteinen.
/// </summary>
public partial class CollectionViewModel : ObservableObject
{
    private readonly ICollectionService _collectionService;
    private readonly ILocalizationService _localizationService;
    private readonly IBattlePassService _battlePassService;
    private readonly ILeagueService _leagueService;
    private readonly IAchievementService _achievementService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
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
    private int _totalProgressPercent;

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
        Title = _localizationService.GetString("CollectionTitle") ?? "Sammlung";

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
        if (!entry.IsDiscovered)
        {
            return new CollectionDisplayItem
            {
                Id = entry.Id,
                Name = "???",
                IconName = "HelpCircleOutline",
                IsDiscovered = false,
                StatText = "???",
                BadgeText = "",
                BadgeColor = "#808080",
                Entry = entry
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
            BadgeText = badgeText,
            BadgeColor = badgeColor,
            Entry = entry
        };
    }

    private string GetStatText(CollectionEntry entry, CollectionCategory category)
    {
        return category switch
        {
            CollectionCategory.Enemies => $"x{entry.TimesDefeated}",
            CollectionCategory.Bosses => $"x{entry.TimesDefeated}",
            CollectionCategory.PowerUps => $"x{entry.TimesCollected}",
            CollectionCategory.BombCards => entry.IsOwned
                ? (_localizationService.GetString("CollectionOwned") ?? "Besessen")
                : (_localizationService.GetString("CollectionNotOwned") ?? "-"),
            CollectionCategory.Cosmetics => entry.IsOwned
                ? (_localizationService.GetString("CollectionOwned") ?? "Besessen")
                : (_localizationService.GetString("CollectionNotOwned") ?? "-"),
            _ => ""
        };
    }

    private (string Text, string Color) GetBadgeInfo(CollectionEntry entry, CollectionCategory category)
    {
        if (category is CollectionCategory.BombCards or CollectionCategory.Cosmetics)
        {
            if (entry.IsOwned)
                return (_localizationService.GetString("CollectionOwned") ?? "Besessen", "#4CAF50");
            return (_localizationService.GetString("CollectionNotOwned") ?? "-", "#2196F3");
        }

        return ("", "#2196F3");
    }

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

        ProgressText = $"{discovered}/{total} ({categoryPercent}%)";

        TotalProgressPercent = _collectionService.GetTotalProgressPercent();
        TotalProgressText = $"{TotalProgressPercent}%";
    }

    private void LoadMilestones()
    {
        var milestones = _collectionService.GetMilestones();
        var claimText = _localizationService.GetString("CollectionClaimNow") ?? "Abholen";

        Milestones.Clear();
        foreach (var ms in milestones)
        {
            var rewardParts = new List<string>();
            if (ms.CoinReward > 0)
                rewardParts.Add($"{ms.CoinReward:N0} Coins");
            if (ms.GemReward > 0)
                rewardParts.Add($"{ms.GemReward} Gems");

            Milestones.Add(new MilestoneDisplayItem
            {
                PercentRequired = ms.PercentRequired,
                PercentLabel = $"{ms.PercentRequired}%",
                RewardText = string.Join(" + ", rewardParts),
                ClaimText = claimText,
                IsClaimed = ms.IsClaimed,
                IsReached = ms.IsReached,
                CanClaim = ms.IsReached && !ms.IsClaimed,
                IsLocked = !ms.IsReached && !ms.IsClaimed
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
        NavigationRequested?.Invoke("..");
    }

    [RelayCommand]
    private void SelectCategory(int index)
    {
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
            var label = _localizationService.GetString("CollectionEncountered") ?? "Begegnungen";
            parts.Add($"{label}: {entry.TimesEncountered}");
        }

        if (entry.TimesDefeated > 0)
        {
            var label = _localizationService.GetString("CollectionDefeated") ?? "Besiegt";
            parts.Add($"{label}: {entry.TimesDefeated}");
        }

        if (entry.TimesCollected > 0)
        {
            var label = _localizationService.GetString("CollectionCollected") ?? "Gesammelt";
            parts.Add($"{label}: {entry.TimesCollected}");
        }

        if (entry.IsOwned)
        {
            parts.Add(_localizationService.GetString("CollectionOwned") ?? "Besessen");
        }

        return string.Join("\n", parts);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// DISPLAY MODELS
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Anzeige-Item für die Sammlungs-Liste</summary>
public class CollectionDisplayItem
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string IconName { get; init; } = "HelpCircleOutline";
    public bool IsDiscovered { get; init; }
    public string StatText { get; init; } = "";
    public string BadgeText { get; init; } = "";
    public string BadgeColor { get; init; } = "#808080";
    public CollectionEntry? Entry { get; init; }
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
}
