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
/// ViewModel for the research skill tree.
/// Shows 3 branches (Tools, Management, Marketing) with 15 levels each.
/// Handles starting/cancelling research and progress updates.
/// </summary>
public partial class ResearchViewModel : ObservableObject
{
    private readonly IResearchService _researchService;
    private readonly IGameStateService _gameStateService;
    private readonly ILocalizationService _localizationService;
    private readonly IRewardedAdService _rewardedAdService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event EventHandler<string>? NavigationRequested;

    /// <summary>
    /// Event to show an alert dialog. Parameters: title, message, buttonText.
    /// </summary>
    public event Action<string, string, string>? AlertRequested;

    /// <summary>
    /// Event to request a confirmation dialog.
    /// Parameters: title, message, acceptText, cancelText. Returns bool.
    /// </summary>
    public event Func<string, string, string, string, Task<bool>>? ConfirmationRequested;

    /// <summary>
    /// Event für die Celebration-Animation bei abgeschlossener Forschung.
    /// Parameters: branch, bonusText.
    /// </summary>
    public event EventHandler<(ResearchBranch Branch, string BonusText)>? CelebrationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private List<ResearchDisplayItem> _toolsBranch = [];

    [ObservableProperty]
    private List<ResearchDisplayItem> _managementBranch = [];

    [ObservableProperty]
    private List<ResearchDisplayItem> _marketingBranch = [];

    [ObservableProperty]
    private Research? _activeResearch;

    [ObservableProperty]
    private double _activeResearchProgress;

    [ObservableProperty]
    private string _activeResearchTimeRemaining = string.Empty;

    [ObservableProperty]
    private string _activeResearchName = string.Empty;

    [ObservableProperty]
    private bool _hasActiveResearch;

    [ObservableProperty]
    private string _currentBalance = "0 \u20AC";

    [ObservableProperty]
    private string _goldenScrewsDisplay = "0";

    [ObservableProperty]
    private bool _canInstantFinish;

    [ObservableProperty]
    private int _instantFinishCost;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _toolsBranchLabel = string.Empty;

    [ObservableProperty]
    private string _managementBranchLabel = string.Empty;

    [ObservableProperty]
    private string _marketingBranchLabel = string.Empty;

    [ObservableProperty]
    private List<ResearchDisplayItem> _selectedBranch = [];

    [ObservableProperty]
    private string _selectedBranchDescription = string.Empty;

    [ObservableProperty]
    private ResearchBranch _selectedTab = ResearchBranch.Tools;

    [ObservableProperty]
    private string _researchRunningLabel = "Forschung l\u00e4uft...";

    // ═══════════════════════════════════════════════════════════════════════
    // COMPUTED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════

    public double ToolsTabOpacity => SelectedTab == ResearchBranch.Tools ? 1.0 : 0.5;
    public double ManagementTabOpacity => SelectedTab == ResearchBranch.Management ? 1.0 : 0.5;
    public double MarketingTabOpacity => SelectedTab == ResearchBranch.Marketing ? 1.0 : 0.5;

    partial void OnSelectedTabChanged(ResearchBranch value)
    {
        OnPropertyChanged(nameof(ToolsTabOpacity));
        OnPropertyChanged(nameof(ManagementTabOpacity));
        OnPropertyChanged(nameof(MarketingTabOpacity));
        UpdateSelectedBranch();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ob der Ad-Speedup-Button sichtbar sein soll (aktive Forschung vorhanden + Werbung aktiv).
    /// </summary>
    public bool CanWatchAdToFinish => HasActiveResearch && _rewardedAdService != null;

    partial void OnHasActiveResearchChanged(bool value) => OnPropertyChanged(nameof(CanWatchAdToFinish));

    public ResearchViewModel(
        IResearchService researchService,
        IGameStateService gameStateService,
        ILocalizationService localizationService,
        IRewardedAdService rewardedAdService)
    {
        _researchService = researchService;
        _gameStateService = gameStateService;
        _localizationService = localizationService;
        _rewardedAdService = rewardedAdService;

        _researchService.ResearchCompleted += OnResearchCompleted;

        UpdateLocalizedTexts();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Loads the entire research tree and populates all 3 branches.
    /// </summary>
    public void LoadResearchTree()
    {
        CurrentBalance = MoneyFormatter.Format(_gameStateService.State.Money, 2);
        GoldenScrewsDisplay = _gameStateService.State.GoldenScrews.ToString("N0");

        ToolsBranch = BuildBranchDisplayItems(ResearchBranch.Tools);
        ManagementBranch = BuildBranchDisplayItems(ResearchBranch.Management);
        MarketingBranch = BuildBranchDisplayItems(ResearchBranch.Marketing);
        UpdateSelectedBranch();

        var active = _researchService.GetActiveResearch();
        ActiveResearch = active;
        HasActiveResearch = active != null;

        if (active != null)
        {
            ActiveResearchName = _localizationService.GetString(active.NameKey);
            CanInstantFinish = active.CanInstantFinish && _gameStateService.CanAffordGoldenScrews(active.InstantFinishScrewCost);
            InstantFinishCost = active.InstantFinishScrewCost;
            UpdateTimer();
        }
        else
        {
            ActiveResearchProgress = 0;
            ActiveResearchTimeRemaining = string.Empty;
            ActiveResearchName = string.Empty;
            CanInstantFinish = false;
            InstantFinishCost = 0;
        }
    }

    /// <summary>
    /// Updates the active research progress and countdown.
    /// Called every second from the game loop.
    /// </summary>
    public void UpdateTimer()
    {
        var active = _researchService.GetActiveResearch();
        ActiveResearch = active;
        HasActiveResearch = active != null;

        if (active == null)
        {
            ActiveResearchProgress = 0;
            ActiveResearchTimeRemaining = string.Empty;
            ActiveResearchName = string.Empty;
            return;
        }

        // Progress ist 0-100 (Prozent), ProgressBar hat Maximum=1 → durch 100 teilen
        ActiveResearchProgress = active.Progress / 100.0;
        ActiveResearchName = _localizationService.GetString(active.NameKey);

        var remaining = active.RemainingTime;
        if (remaining.HasValue && remaining.Value > TimeSpan.Zero)
        {
            var r = remaining.Value;
            if (r.TotalHours >= 1)
            {
                ActiveResearchTimeRemaining = $"{(int)r.TotalHours:D2}:{r.Minutes:D2}:{r.Seconds:D2}";
            }
            else
            {
                ActiveResearchTimeRemaining = $"{r.Minutes:D2}:{r.Seconds:D2}";
            }
        }
        else
        {
            ActiveResearchTimeRemaining = _localizationService.GetString("Completing");
        }
    }

    /// <summary>
    /// Updates localized texts after language change.
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("Research");
        ToolsBranchLabel = $"{ResearchBranch.Tools.GetIcon()} {_localizationService.GetString(ResearchBranch.Tools.GetLocalizationKey())}";
        ManagementBranchLabel = $"{ResearchBranch.Management.GetIcon()} {_localizationService.GetString(ResearchBranch.Management.GetLocalizationKey())}";
        MarketingBranchLabel = $"{ResearchBranch.Marketing.GetIcon()} {_localizationService.GetString(ResearchBranch.Marketing.GetLocalizationKey())}";
        ResearchRunningLabel = _localizationService.GetString("ResearchRunning") ?? "Forschung l\u00e4uft...";
        LoadResearchTree();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void SelectToolsBranch()
    {
        SelectedTab = ResearchBranch.Tools;
    }

    [RelayCommand]
    private void SelectManagementBranch()
    {
        SelectedTab = ResearchBranch.Management;
    }

    [RelayCommand]
    private void SelectMarketingBranch()
    {
        SelectedTab = ResearchBranch.Marketing;
    }

    [RelayCommand]
    private async Task StartResearchAsync(string? researchId)
    {
        if (string.IsNullOrEmpty(researchId)) return;

        if (HasActiveResearch)
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("ResearchInProgress"),
                _localizationService.GetString("ResearchInProgressDesc"),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        // Forschung suchen
        var allResearch = _researchService.GetResearchTree();
        var target = allResearch.FirstOrDefault(r => r.Id == researchId);
        if (target == null) return;

        // Bereits erforscht oder aktiv → ignorieren
        if (target.IsResearched || target.IsActive) return;

        // Voraussetzungen prüfen
        bool prerequisitesMet = target.Prerequisites.All(p => _researchService.IsResearched(p));
        if (!prerequisitesMet)
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("ResearchLocked"),
                _localizationService.GetString("ResearchLockedDesc"),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        // Kosten prüfen
        if (!_gameStateService.CanAfford(target.Cost))
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NotEnoughMoney"),
                string.Format(_localizationService.GetString("ResearchCostFormat"), MoneyFormatter.Format(target.Cost, 0)),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        // Bestätigungsdialog mit Details anzeigen
        string name = _localizationService.GetString(target.NameKey);
        string effectDesc = _localizationService.GetString(target.DescriptionKey);
        string costText = MoneyFormatter.Format(target.Cost, 0);
        string durationText = FormatDuration(target.Duration);

        // Dialog-Body zusammenbauen
        string body = $"{effectDesc}\n\n" +
                       $"{_localizationService.GetString("ResearchConfirmCost")}: {costText}\n" +
                       $"{_localizationService.GetString("ResearchConfirmDuration")}: {durationText}";

        bool confirm = true;
        if (ConfirmationRequested != null)
        {
            confirm = await ConfirmationRequested.Invoke(
                name,
                body,
                _localizationService.GetString("StartResearch"),
                _localizationService.GetString("Cancel"));
        }

        if (!confirm) return;

        bool success = _researchService.StartResearch(researchId);
        if (success)
        {
            LoadResearchTree();
        }
        else
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("ResearchFailed"),
                _localizationService.GetString("ResearchFailedDesc"),
                _localizationService.GetString("OK") ?? "OK");
        }
    }

    [RelayCommand]
    private async Task InstantFinishResearchAsync()
    {
        if (!HasActiveResearch || ActiveResearch == null || !ActiveResearch.CanInstantFinish) return;

        var cost = ActiveResearch.InstantFinishScrewCost;

        if (!_gameStateService.CanAffordGoldenScrews(cost))
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NotEnoughScrews"),
                string.Format(_localizationService.GetString("NotEnoughScrewsDesc"), cost),
                _localizationService.GetString("OK") ?? "OK");
            return;
        }

        bool confirm = true;
        if (ConfirmationRequested != null)
        {
            confirm = await ConfirmationRequested.Invoke(
                _localizationService.GetString("InstantFinish"),
                string.Format(_localizationService.GetString("InstantFinishDesc"), cost),
                _localizationService.GetString("Confirm"),
                _localizationService.GetString("Cancel"));
        }

        if (!confirm) return;

        if (_researchService.InstantFinishResearch())
        {
            LoadResearchTree();
        }
    }

    [RelayCommand]
    private async Task WatchAdToFinishResearchAsync()
    {
        if (!HasActiveResearch || ActiveResearch == null) return;

        // NameKey VOR Abschluss speichern (LoadResearchTree setzt ActiveResearch = null)
        var nameKey = ActiveResearch.NameKey;

        var success = await _rewardedAdService.ShowAdAsync("research_speedup");
        if (success)
        {
            _researchService.InstantFinishResearch();
            LoadResearchTree();

            AlertRequested?.Invoke(
                _localizationService.GetString("ResearchFinishedFree"),
                _localizationService.GetString(nameKey),
                _localizationService.GetString("Great"));
        }
    }

    [RelayCommand]
    private async Task CancelResearchAsync()
    {
        if (!HasActiveResearch || ActiveResearch == null) return;

        bool confirm = true;
        if (ConfirmationRequested != null)
        {
            confirm = await ConfirmationRequested.Invoke(
                _localizationService.GetString("CancelResearch"),
                _localizationService.GetString("CancelResearchDesc"),
                _localizationService.GetString("Confirm"),
                _localizationService.GetString("Cancel"));
        }

        if (!confirm) return;

        bool success = _researchService.CancelResearch();
        if (success)
        {
            LoadResearchTree();
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        NavigationRequested?.Invoke(this, "..");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdateSelectedBranch()
    {
        SelectedBranch = SelectedTab switch
        {
            ResearchBranch.Tools => ToolsBranch,
            ResearchBranch.Management => ManagementBranch,
            ResearchBranch.Marketing => MarketingBranch,
            _ => ToolsBranch
        };

        SelectedBranchDescription = _localizationService.GetString(SelectedTab.GetDescriptionKey());
    }

    private List<ResearchDisplayItem> BuildBranchDisplayItems(ResearchBranch branch)
    {
        var branchResearches = _researchService.GetBranch(branch);

        return branchResearches.Select(r =>
        {
            // Pruefen ob Voraussetzungen erfuellt
            bool prerequisitesMet = r.Prerequisites.All(p => _researchService.IsResearched(p));
            bool canStart = !r.IsResearched && !r.IsActive && prerequisitesMet && !HasActiveResearch
                            && _gameStateService.CanAfford(r.Cost);

            return new ResearchDisplayItem
            {
                Id = r.Id,
                Name = _localizationService.GetString(r.NameKey),
                Description = _localizationService.GetString(r.DescriptionKey),
                Level = r.Level,
                Cost = r.Cost,
                CostDisplay = MoneyFormatter.Format(r.Cost, 0),
                Duration = r.Duration,
                DurationDisplay = FormatDuration(r.Duration),
                IsResearched = r.IsResearched,
                IsActive = r.IsActive,
                IsLocked = !prerequisitesMet,
                CanStart = canStart,
                // Progress ist 0-100, ProgressBar hat Maximum=1 → normalisieren
                Progress = r.Progress / 100.0,
                Effect = r.Effect,
                InstantFinishScrewCost = r.InstantFinishScrewCost
            };
        }).ToList();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        return $"{duration.Minutes}m";
    }

    private void OnResearchCompleted(object? sender, Research research)
    {
        LoadResearchTree();

        // Celebration-Animation triggern (Confetti + Glow + Bonus-Text)
        string bonusText = _localizationService.GetString(research.NameKey);
        CelebrationRequested?.Invoke(this, (research.Branch, bonusText));

        AlertRequested?.Invoke(
            _localizationService.GetString("ResearchComplete"),
            string.Format(
                _localizationService.GetString("ResearchCompleteFormat"),
                _localizationService.GetString(research.NameKey)),
            _localizationService.GetString("Great"));
    }
}

/// <summary>
/// Display item for a single research node in the skill tree.
/// </summary>
public class ResearchDisplayItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Level { get; set; }
    public decimal Cost { get; set; }
    public string CostDisplay { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string DurationDisplay { get; set; } = string.Empty;
    public bool IsResearched { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public bool CanStart { get; set; }
    public double Progress { get; set; }
    public ResearchEffect Effect { get; set; } = new();

    /// <summary>
    /// Opacity: locked = 0.4, unlocked = 1.0.
    /// </summary>
    /// <summary>
    /// Display label for the level (e.g. "Lv.3").
    /// </summary>
    public string LevelDisplay => $"Lv.{Level}";

    /// <summary>
    /// Goldschrauben-Kosten fuer Sofortfertigstellung (0 = nicht verfuegbar).
    /// </summary>
    public int InstantFinishScrewCost { get; set; }

    /// <summary>
    /// Ob Sofortfertigstellung verfuegbar waere (ab Level 8).
    /// </summary>
    public bool HasInstantFinishOption => InstantFinishScrewCost > 0;

    /// <summary>
    /// Opacity: locked = 0.4, unlocked = 1.0.
    /// </summary>
    public double DisplayOpacity => IsLocked ? 0.4 : 1.0;
}
