using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

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
    private string _title = string.Empty;

    [ObservableProperty]
    private string _toolsBranchLabel = string.Empty;

    [ObservableProperty]
    private string _managementBranchLabel = string.Empty;

    [ObservableProperty]
    private string _marketingBranchLabel = string.Empty;

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public ResearchViewModel(
        IResearchService researchService,
        IGameStateService gameStateService,
        ILocalizationService localizationService)
    {
        _researchService = researchService;
        _gameStateService = gameStateService;
        _localizationService = localizationService;

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

        ToolsBranch = BuildBranchDisplayItems(ResearchBranch.Tools);
        ManagementBranch = BuildBranchDisplayItems(ResearchBranch.Management);
        MarketingBranch = BuildBranchDisplayItems(ResearchBranch.Marketing);

        var active = _researchService.GetActiveResearch();
        ActiveResearch = active;
        HasActiveResearch = active != null;

        if (active != null)
        {
            ActiveResearchName = _localizationService.GetString(active.NameKey);
            UpdateTimer();
        }
        else
        {
            ActiveResearchProgress = 0;
            ActiveResearchTimeRemaining = string.Empty;
            ActiveResearchName = string.Empty;
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

        ActiveResearchProgress = active.Progress;
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
        LoadResearchTree();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void StartResearch(string? researchId)
    {
        if (string.IsNullOrEmpty(researchId)) return;

        if (HasActiveResearch)
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("ResearchInProgress"),
                _localizationService.GetString("ResearchInProgressDesc"),
                "OK");
            return;
        }

        // Kosten pruefen
        var allResearch = _researchService.GetResearchTree();
        var target = allResearch.FirstOrDefault(r => r.Id == researchId);
        if (target == null) return;

        if (!_gameStateService.CanAfford(target.Cost))
        {
            AlertRequested?.Invoke(
                _localizationService.GetString("NotEnoughMoney"),
                string.Format(_localizationService.GetString("ResearchCostFormat"), MoneyFormatter.Format(target.Cost, 0)),
                "OK");
            return;
        }

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
                "OK");
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
                Progress = r.Progress,
                Effect = r.Effect
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
    public double DisplayOpacity => IsLocked ? 0.4 : 1.0;
}
