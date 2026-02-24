using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel für das Multiplayer-Gildensystem via Firebase.
/// Drei UI-Zustände: Offline, Nicht-in-Gilde (Browse+Create), In-Gilde (Details).
/// </summary>
public partial class GuildViewModel : ObservableObject
{
    private readonly IGameStateService _gameStateService;
    private readonly IGuildService _guildService;
    private readonly ILocalizationService _localizationService;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Zustand
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private bool _isInGuild;

    [ObservableProperty]
    private bool _isOffline;

    [ObservableProperty]
    private bool _isLoading;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Spielername-Dialog
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isNameDialogVisible;

    [ObservableProperty]
    private string _nameInput = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Gilde erstellen
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isCreateDialogVisible;

    [ObservableProperty]
    private string _createGuildName = "";

    [ObservableProperty]
    private string _selectedIcon = "ShieldHome";

    [ObservableProperty]
    private string _selectedColor = "#D97706";

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Browse (Nicht in Gilde)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<GuildListItem> _availableGuilds = [];

    [ObservableProperty]
    private bool _hasNoGuilds;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Gilden-Detail (In Gilde)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _currentGuildName = "";

    [ObservableProperty]
    private string _currentGuildIcon = "ShieldHome";

    [ObservableProperty]
    private string _currentGuildColor = "#D97706";

    [ObservableProperty]
    private int _guildLevel;

    [ObservableProperty]
    private string _goalProgressDisplay = "";

    [ObservableProperty]
    private double _goalProgress;

    [ObservableProperty]
    private string _incomeBonusDisplay = "";

    [ObservableProperty]
    private string _incomeBonusDetailDisplay = "";

    [ObservableProperty]
    private string _membersHeaderDisplay = "";

    [ObservableProperty]
    private ObservableCollection<GuildMemberDisplay> _members = [];

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Beitrag-Slider
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private double _contributionPercent = 10;

    [ObservableProperty]
    private string _contributionAmountDisplay = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Gilden-Forschung
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<GuildResearchDisplay> _guildResearch = [];

    [ObservableProperty]
    private bool _hasGuildResearch;

    [ObservableProperty]
    private string _guildResearchSummary = "";

    [ObservableProperty]
    private string _maxMembersDisplay = "";

    [ObservableProperty]
    private bool _isResearchContributeDialogVisible;

    [ObservableProperty]
    private string _selectedResearchId = "";

    [ObservableProperty]
    private string _selectedResearchName = "";

    [ObservableProperty]
    private double _researchContributePercent = 10;

    [ObservableProperty]
    private string _researchContributeDisplay = "";

    // ═══════════════════════════════════════════════════════════════════════
    // ICON + FARB-AUSWAHL
    // ═══════════════════════════════════════════════════════════════════════

    public static List<string> AvailableIcons =>
    [
        "ShieldHome", "Hammer", "Wrench", "LightningBolt",
        "OfficeBuildingCog", "Pencil", "HardHat", "Saw",
        "FormatPaint", "AccountGroup"
    ];

    public static List<string> AvailableColors =>
    [
        "#D97706", "#8B4513", "#757575", "#FFC107",
        "#795548", "#4CAF50", "#2196F3", "#E91E63"
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════════════════

    public GuildViewModel(
        IGameStateService gameStateService,
        IGuildService guildService,
        ILocalizationService localizationService)
    {
        _gameStateService = gameStateService;
        _guildService = guildService;
        _localizationService = localizationService;

        _guildService.GuildUpdated += OnGuildUpdated;

        UpdateLocalizedTexts();
        RefreshFromLocalState();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Navigation + Laden
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task LoadGuildDataAsync()
    {
        IsLoading = true;
        try
        {
            // Spielername prüfen
            if (string.IsNullOrEmpty(_guildService.PlayerName))
            {
                IsNameDialogVisible = true;
                return;
            }

            await _guildService.InitializeAsync();
            IsOffline = !_guildService.IsOnline;

            var membership = _gameStateService.State.GuildMembership;
            IsInGuild = membership != null;

            if (IsInGuild)
            {
                UpdateContributionDisplay();
                await RefreshGuildDetailsAsync();
            }
            else
            {
                await LoadAvailableGuildsAsync();
            }
        }
        catch
        {
            IsOffline = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        await LoadGuildDataAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Spielername
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ConfirmNameAsync()
    {
        if (string.IsNullOrWhiteSpace(NameInput)) return;

        _guildService.SetPlayerName(NameInput.Trim());
        IsNameDialogVisible = false;

        // Nach Namenseingabe normal laden
        await LoadGuildDataAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Gilde erstellen
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ShowCreateDialog()
    {
        CreateGuildName = "";
        SelectedIcon = "ShieldHome";
        SelectedColor = "#D97706";
        IsCreateDialogVisible = true;
    }

    [RelayCommand]
    private void CancelCreate()
    {
        IsCreateDialogVisible = false;
    }

    [RelayCommand]
    private async Task CreateGuildAsync()
    {
        if (string.IsNullOrWhiteSpace(CreateGuildName)) return;

        IsLoading = true;
        try
        {
            var success = await _guildService.CreateGuildAsync(CreateGuildName.Trim(), SelectedIcon, SelectedColor);
            if (success)
            {
                IsCreateDialogVisible = false;
                IsInGuild = true;
                RefreshFromLocalState();
                await RefreshGuildDetailsAsync();

                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("GuildCreated") ?? "Gilde erfolgreich erstellt!");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectIcon(string icon)
    {
        SelectedIcon = icon;
    }

    [RelayCommand]
    private void SelectColor(string color)
    {
        SelectedColor = color;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Beitreten / Verlassen / Beitragen
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task JoinGuildAsync(GuildListItem? item)
    {
        if (item == null) return;

        IsLoading = true;
        try
        {
            var success = await _guildService.JoinGuildAsync(item.Id);
            if (success)
            {
                IsInGuild = true;
                RefreshFromLocalState();
                await RefreshGuildDetailsAsync();

                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("GuildJoined") ?? "Gilde beigetreten!");
            }
            else
            {
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("GuildFull") ?? "Gilde ist voll.");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LeaveGuildAsync()
    {
        IsLoading = true;
        try
        {
            var success = await _guildService.LeaveGuildAsync();
            if (success)
            {
                IsInGuild = false;
                Members.Clear();
                RefreshFromLocalState();
                await LoadAvailableGuildsAsync();

                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("GuildLeft") ?? "Gilde verlassen.");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ContributeAsync()
    {
        if (!IsInGuild) return;

        var money = _gameStateService.State.Money;
        var contribution = money * (decimal)ContributionPercent / 100m;
        if (contribution < 100m || money < contribution) return;

        var success = await _guildService.ContributeAsync(contribution);
        if (success)
        {
            // Slider-Anzeige aktualisieren (Geld hat sich geändert)
            UpdateContributionDisplay();

            // Gilden-Daten aktualisieren
            await RefreshGuildDetailsAsync();

            MessageRequested?.Invoke(
                _localizationService.GetString("Guild") ?? "Innung",
                string.Format(
                    _localizationService.GetString("GuildContributed") ?? "€{0} beigetragen!",
                    MoneyFormatter.Format(contribution, 0)));
        }
    }

    partial void OnContributionPercentChanged(double value)
    {
        UpdateContributionDisplay();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Gilden-Forschung
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ShowResearchContributeDialog(GuildResearchDisplay? research)
    {
        if (research == null || !research.IsActive) return;

        SelectedResearchId = research.Id;
        SelectedResearchName = research.Name;
        ResearchContributePercent = 10;
        UpdateResearchContributeDisplay();
        IsResearchContributeDialogVisible = true;
    }

    [RelayCommand]
    private void CancelResearchContribute()
    {
        IsResearchContributeDialogVisible = false;
    }

    [RelayCommand]
    private async Task ConfirmResearchContributeAsync()
    {
        if (string.IsNullOrEmpty(SelectedResearchId)) return;

        var money = _gameStateService.State.Money;
        var amount = (long)(money * (decimal)ResearchContributePercent / 100m);
        if (amount < 100) return;

        IsResearchContributeDialogVisible = false;
        IsLoading = true;

        try
        {
            var success = await _guildService.ContributeToResearchAsync(SelectedResearchId, amount);
            if (success)
            {
                MessageRequested?.Invoke(
                    _localizationService.GetString("GuildResearchTitle") ?? "Gilden-Forschung",
                    string.Format(
                        _localizationService.GetString("GuildResearchContributed") ?? "€{0} beigetragen!",
                        MoneyFormatter.Format(amount, 0)));

                // Forschungsliste aktualisieren
                await LoadGuildResearchAsync();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnResearchContributePercentChanged(double value)
    {
        UpdateResearchContributeDisplay();
    }

    private void UpdateResearchContributeDisplay()
    {
        var money = _gameStateService.State.Money;
        var amount = money * (decimal)ResearchContributePercent / 100m;
        ResearchContributeDisplay = $"{MoneyFormatter.Format(amount, 0)} \u20AC ({ResearchContributePercent:F0}%)";
    }

    private void UpdateContributionDisplay()
    {
        var money = _gameStateService.State.Money;
        var amount = money * (decimal)ContributionPercent / 100m;
        ContributionAmountDisplay = $"{MoneyFormatter.Format(amount, 0)} \u20AC ({ContributionPercent:F0}%)";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Wird vom MainViewModel aufgerufen wenn der Guild-Tab aktiviert wird.
    /// Lädt Daten asynchron von Firebase.
    /// </summary>
    public void RefreshGuild()
    {
        RefreshFromLocalState();
        _ = LoadGuildDataCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Lokalisierte Texte aktualisieren.
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("Guild") ?? "Innung";
        RefreshFromLocalState();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE METHODS
    // ═══════════════════════════════════════════════════════════════════════

    private void RefreshFromLocalState()
    {
        var membership = _gameStateService.State.GuildMembership;
        IsInGuild = membership != null;

        if (membership != null)
        {
            CurrentGuildName = membership.GuildName;
            CurrentGuildIcon = membership.GuildIcon;
            CurrentGuildColor = membership.GuildColor;
            GuildLevel = membership.GuildLevel;

            var bonus = membership.IncomeBonus;
            IncomeBonusDisplay = $"+{bonus * 100:F0}%";
            IncomeBonusDetailDisplay = $"+{bonus * 100:F0}% {(_localizationService.GetString("GuildIncomeBonus") ?? "Einkommens-Bonus")}";
        }
        else
        {
            CurrentGuildName = "";
            GuildLevel = 0;
            IncomeBonusDisplay = "";
            IncomeBonusDetailDisplay = "";
            GoalProgress = 0;
            GoalProgressDisplay = "";
            MembersHeaderDisplay = "";
        }
    }

    private async Task RefreshGuildDetailsAsync()
    {
        var detail = await _guildService.RefreshGuildDetailsAsync();
        if (detail == null)
        {
            // Gilde existiert nicht mehr
            IsInGuild = false;
            RefreshFromLocalState();
            return;
        }

        CurrentGuildName = detail.Name;
        CurrentGuildIcon = detail.Icon;
        CurrentGuildColor = detail.Color;
        GuildLevel = detail.Level;
        GoalProgress = detail.WeeklyGoalProgress;
        GoalProgressDisplay = $"{MoneyFormatter.Format(detail.WeeklyProgress, 0)} / {MoneyFormatter.Format(detail.WeeklyGoal, 0)}";

        var bonus = detail.IncomeBonus;
        IncomeBonusDisplay = $"+{bonus * 100:F0}%";
        IncomeBonusDetailDisplay = $"+{bonus * 100:F0}% {(_localizationService.GetString("GuildIncomeBonus") ?? "Einkommens-Bonus")}";

        // Mitglieder aktualisieren
        var memberDisplays = new ObservableCollection<GuildMemberDisplay>();
        foreach (var m in detail.Members)
        {
            memberDisplays.Add(new GuildMemberDisplay
            {
                Name = m.Name,
                RoleDisplay = m.Role == "leader"
                    ? (_localizationService.GetString("GuildLeaderRole") ?? "Gründer")
                    : (_localizationService.GetString("GuildMemberRole") ?? "Mitglied"),
                ContributionDisplay = MoneyFormatter.Format(m.Contribution, 0),
                IsPlayer = m.IsCurrentPlayer
            });
        }
        Members = memberDisplays;
        MembersHeaderDisplay = $"{(_localizationService.GetString("Members") ?? "Mitglieder")} ({Members.Count})";

        // Gilden-Forschungen laden
        await LoadGuildResearchAsync();
    }

    private async Task LoadGuildResearchAsync()
    {
        var items = await _guildService.GetGuildResearchAsync();

        // Namen und Beschreibungen lokalisieren
        foreach (var item in items)
        {
            item.Name = _localizationService.GetString(item.Name) ?? item.Name;
            item.Description = _localizationService.GetString(item.Description) ?? item.Description;
        }

        GuildResearch = new ObservableCollection<GuildResearchDisplay>(items);
        HasGuildResearch = GuildResearch.Count > 0;

        var completed = items.Count(i => i.IsCompleted);
        GuildResearchSummary = $"{completed}/{items.Count}";

        var maxMembers = _guildService.GetMaxMembers();
        MaxMembersDisplay = $"Max. {maxMembers}";
    }

    private async Task LoadAvailableGuildsAsync()
    {
        var guilds = await _guildService.BrowseGuildsAsync();
        AvailableGuilds = new ObservableCollection<GuildListItem>(guilds);
        HasNoGuilds = AvailableGuilds.Count == 0;
    }

    private void OnGuildUpdated()
    {
        RefreshFromLocalState();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// DISPLAY MODELS
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Anzeige-Modell für ein Gildenmitglied im UI.
/// </summary>
public class GuildMemberDisplay
{
    public string Name { get; set; } = "";
    public string RoleDisplay { get; set; } = "";
    public string ContributionDisplay { get; set; } = "";
    public bool IsPlayer { get; set; }
}
