using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// Zustand der Gilden-Ansicht. Genau einer ist aktiv (mutual exclusive Panels).
/// </summary>
public enum GuildViewState
{
    Loading,
    Offline,
    NameDialog,
    CreateDialog,
    Browse,
    InGuild
}

/// <summary>
/// ViewModel für das Multiplayer-Gildensystem via Firebase.
/// Sechs UI-Zustände via GuildViewState Enum (flache Panels, keine verschachtelte IsVisible-Logik).
/// </summary>
public partial class GuildViewModel : ObservableObject
{
    private readonly IGameStateService _gameStateService;
    private readonly IGuildService _guildService;
    private readonly ILocalizationService _localizationService;
    private bool _isBusy;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;
    public event Func<string, string, string, string, Task<bool>>? ConfirmationRequested;
    public event Action? CelebrationRequested;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - ViewState (zentraler UI-Zustand)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadingState))]
    [NotifyPropertyChangedFor(nameof(IsOfflineState))]
    [NotifyPropertyChangedFor(nameof(IsNameDialogState))]
    [NotifyPropertyChangedFor(nameof(IsCreateDialogState))]
    [NotifyPropertyChangedFor(nameof(IsBrowseState))]
    [NotifyPropertyChangedFor(nameof(IsInGuildState))]
    private GuildViewState _viewState = GuildViewState.Loading;

    /// <summary>Lade-Spinner sichtbar.</summary>
    public bool IsLoadingState => ViewState == GuildViewState.Loading;

    /// <summary>Offline-Hinweis sichtbar.</summary>
    public bool IsOfflineState => ViewState == GuildViewState.Offline;

    /// <summary>Spielername-Dialog sichtbar.</summary>
    public bool IsNameDialogState => ViewState == GuildViewState.NameDialog;

    /// <summary>Gilde-erstellen-Dialog sichtbar.</summary>
    public bool IsCreateDialogState => ViewState == GuildViewState.CreateDialog;

    /// <summary>Gilden-Browse-Liste sichtbar (nicht in Gilde, kein Dialog offen).</summary>
    public bool IsBrowseState => ViewState == GuildViewState.Browse;

    /// <summary>Gilden-Detail sichtbar (Spieler ist Mitglied).</summary>
    public bool IsInGuildState => ViewState == GuildViewState.InGuild;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Spielername-Dialog
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _nameInput = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Gilde erstellen
    // ═══════════════════════════════════════════════════════════════════════

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
    // PROPERTIES - Einladungs-System
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _guildInviteCode = "";

    [ObservableProperty]
    private string _joinCodeInput = "";

    [ObservableProperty]
    private ObservableCollection<AvailablePlayerDisplay> _availablePlayers = [];

    [ObservableProperty]
    private bool _hasNoAvailablePlayers;

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
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            await LoadGuildDataInternalAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>
    /// Interne Lade-Logik ohne isBusy-Guard.
    /// Wird von LoadGuildDataAsync (mit Guard) und internen Callern (bereits im isBusy-Kontext) verwendet.
    /// </summary>
    private async Task LoadGuildDataInternalAsync()
    {
        try
        {
            ViewState = GuildViewState.Loading;
            // Spielername prüfen
            if (string.IsNullOrEmpty(_guildService.PlayerName))
            {
                ViewState = GuildViewState.NameDialog;
                return;
            }

            await _guildService.InitializeAsync();

            if (!_guildService.IsOnline)
            {
                ViewState = GuildViewState.Offline;
                return;
            }

            var membership = _gameStateService.State.GuildMembership;

            if (membership != null)
            {
                UpdateContributionDisplay();
                await RefreshGuildDetailsAsync();
                ViewState = GuildViewState.InGuild;
            }
            else
            {
                // Spieler als verfügbar registrieren (für Einladungs-Browser)
                await _guildService.RegisterAsAvailableAsync();

                await LoadAvailableGuildsAsync();
                ViewState = GuildViewState.Browse;
            }
        }
        catch
        {
            ViewState = GuildViewState.Offline;
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
        ViewState = GuildViewState.CreateDialog;
    }

    [RelayCommand]
    private void CancelCreate()
    {
        ViewState = GuildViewState.Browse;
    }

    [RelayCommand]
    private async Task CreateGuildAsync()
    {
        if (string.IsNullOrWhiteSpace(CreateGuildName)) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            ViewState = GuildViewState.Loading;
            var success = await _guildService.CreateGuildAsync(CreateGuildName.Trim(), SelectedIcon, SelectedColor);
            if (success)
            {
                RefreshFromLocalState();
                await RefreshGuildDetailsAsync();
                ViewState = GuildViewState.InGuild;
                CelebrationRequested?.Invoke();

                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("GuildCreated") ?? "Gilde erfolgreich erstellt!");
            }
            else
            {
                ViewState = GuildViewState.Browse;
            }
        }
        catch
        {
            ViewState = GuildViewState.Browse;
        }
        finally
        {
            _isBusy = false;
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
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            ViewState = GuildViewState.Loading;
            var success = await _guildService.JoinGuildAsync(item.Id);
            if (success)
            {
                RefreshFromLocalState();
                await RefreshGuildDetailsAsync();
                ViewState = GuildViewState.InGuild;
                CelebrationRequested?.Invoke();

                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("GuildJoined") ?? "Gilde beigetreten!");
            }
            else
            {
                ViewState = GuildViewState.Browse;
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("GuildFull") ?? "Gilde ist voll.");
            }
        }
        catch
        {
            ViewState = GuildViewState.Browse;
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task LeaveGuildAsync()
    {
        if (_isBusy) return;
        if (ConfirmationRequested != null)
        {
            var confirmed = await ConfirmationRequested.Invoke(
                _localizationService.GetString("LeaveGuildTitle") ?? "Gilde verlassen",
                _localizationService.GetString("LeaveGuildConfirm") ?? "Willst du die Gilde wirklich verlassen?",
                _localizationService.GetString("Leave") ?? "Verlassen",
                _localizationService.GetString("Cancel") ?? "Abbrechen");
            if (!confirmed) return;
        }
        _isBusy = true;
        try
        {
            ViewState = GuildViewState.Loading;
            var success = await _guildService.LeaveGuildAsync();
            if (success)
            {
                Members.Clear();
                RefreshFromLocalState();
                await LoadAvailableGuildsAsync();
                ViewState = GuildViewState.Browse;

                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("GuildLeft") ?? "Gilde verlassen.");
            }
            else
            {
                ViewState = GuildViewState.InGuild;
            }
        }
        catch
        {
            ViewState = GuildViewState.InGuild;
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task ContributeAsync()
    {
        if (ViewState != GuildViewState.InGuild) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
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
        finally
        {
            _isBusy = false;
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
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            var money = _gameStateService.State.Money;
            var amount = (long)(money * (decimal)ResearchContributePercent / 100m);
            if (amount < 100) return;

            IsResearchContributeDialogVisible = false;
            ViewState = GuildViewState.Loading;

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

            ViewState = GuildViewState.InGuild;
        }
        finally
        {
            _isBusy = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Sub-Seiten Navigation
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void NavigateToResearch() => NavigationRequested?.Invoke("guild_research");

    [RelayCommand]
    private void NavigateToMembers() => NavigationRequested?.Invoke("guild_members");

    [RelayCommand]
    private void NavigateToInvite() => NavigationRequested?.Invoke("guild_invite");

    [RelayCommand]
    private void NavigateBack() => NavigationRequested?.Invoke("..");

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Einladungs-System
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ShareInviteCode()
    {
        if (string.IsNullOrEmpty(GuildInviteCode)) return;

        // TODO Phase 4: UriLauncher.ShareText() implementieren (Android Intent / Desktop Clipboard)
        MessageRequested?.Invoke(
            _localizationService.GetString("Guild") ?? "Innung",
            $"{(_localizationService.GetString("CodeCopied") ?? "Code kopiert")}: {GuildInviteCode}");
    }

    [RelayCommand]
    private async Task JoinByCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(JoinCodeInput)) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            var success = await _guildService.JoinByInviteCodeAsync(JoinCodeInput.Trim());
            if (success)
            {
                JoinCodeInput = "";
                CelebrationRequested?.Invoke();
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("GuildJoined") ?? "Gilde beigetreten!");
                await LoadGuildDataInternalAsync();
            }
            else
            {
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("GuildCodeInvalid") ?? "Ungültiger Code");
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadAvailablePlayersAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            var players = await _guildService.BrowseAvailablePlayersAsync();
            var inviteText = _localizationService.GetString("InvitePlayer") ?? "Einladen";
            var invitedText = _localizationService.GetString("InvitedBadge") ?? "Eingeladen";
            var displays = new ObservableCollection<AvailablePlayerDisplay>();
            foreach (var p in players)
            {
                displays.Add(new AvailablePlayerDisplay
                {
                    Uid = p.Uid,
                    Name = p.Name,
                    LevelDisplay = $"Lv. {p.Level}",
                    LastActiveDisplay = FormatLastActive(p.LastActive),
                    IsInvited = false,
                    InviteButtonText = inviteText,
                    InvitedText = invitedText
                });
            }
            AvailablePlayers = displays;
            HasNoAvailablePlayers = displays.Count == 0;
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task InvitePlayerAsync(AvailablePlayerDisplay? player)
    {
        if (player == null || player.IsInvited) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            // Einladungs-Code teilen (Spieler muss Code selbst eingeben)
            // Wir markieren den Spieler als "eingeladen" als visuelles Feedback
            player.IsInvited = true;

            // Invite-Code sicherstellen
            if (string.IsNullOrEmpty(GuildInviteCode))
            {
                var code = await _guildService.GetOrCreateInviteCodeAsync();
                if (!string.IsNullOrEmpty(code))
                    GuildInviteCode = code;
            }

            MessageRequested?.Invoke(
                _localizationService.GetString("Guild") ?? "Innung",
                _localizationService.GetString("InviteSent") ?? "Einladung gesendet");
        }
        finally
        {
            _isBusy = false;
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
        ResearchContributeDisplay = $"{MoneyFormatter.Format(amount, 0)} ({ResearchContributePercent:F0}%)";
    }

    private void UpdateContributionDisplay()
    {
        var money = _gameStateService.State.Money;
        var amount = money * (decimal)ContributionPercent / 100m;
        ContributionAmountDisplay = $"{MoneyFormatter.Format(amount, 0)} ({ContributionPercent:F0}%)";
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

    /// <summary>
    /// Aktualisiert Gilden-Daten aus dem lokalen GameState und setzt den ViewState.
    /// </summary>
    private void RefreshFromLocalState()
    {
        var membership = _gameStateService.State.GuildMembership;

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
            RefreshFromLocalState();
            ViewState = GuildViewState.Browse;
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

        // Einladungs-Code laden (für Invite-Seite)
        var code = await _guildService.GetOrCreateInviteCodeAsync();
        GuildInviteCode = code ?? "";
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

    /// <summary>
    /// Formatiert den LastActive-Zeitstempel als lesbaren relativen Text.
    /// </summary>
    private string FormatLastActive(string lastActiveIso)
    {
        if (string.IsNullOrEmpty(lastActiveIso)) return "";

        if (!DateTime.TryParse(lastActiveIso, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var lastActive))
            return "";

        var diff = DateTime.UtcNow - lastActive;

        if (diff.TotalHours < 24)
            return _localizationService.GetString("Today") ?? "Heute";
        if (diff.TotalHours < 48)
            return _localizationService.GetString("Yesterday") ?? "Gestern";

        var days = (int)diff.TotalDays;
        var template = _localizationService.GetString("DaysAgo") ?? "vor {0} Tagen";
        return string.Format(template, days);
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

/// <summary>
/// Anzeige-Modell für einen verfügbaren Spieler im Einladungs-Browser.
/// </summary>
public class AvailablePlayerDisplay
{
    public string Uid { get; set; } = "";
    public string Name { get; set; } = "";
    public string LevelDisplay { get; set; } = "";
    public string LastActiveDisplay { get; set; } = "";
    public bool IsInvited { get; set; }
    public string InviteButtonText { get; set; } = "Invite";
    public string InvitedText { get; set; } = "Invited";
}
