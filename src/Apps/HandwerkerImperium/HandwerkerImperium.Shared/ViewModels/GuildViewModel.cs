using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using MeineApps.Core.Ava.Localization;
using MeineApps.Core.Ava.Services;
using MeineApps.Core.Ava.ViewModels;

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
    InGuild,
    // Neue Sub-Seiten (Phase C Gilden-Overhaul)
    WarSeason,
    Boss,
    Hall,
    Achievements
}

/// <summary>
/// ViewModel für das Multiplayer-Gildensystem via Firebase.
/// Sechs UI-Zustände via GuildViewState Enum (flache Panels, keine verschachtelte IsVisible-Logik).
/// </summary>
public sealed partial class GuildViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private readonly IGameStateService _gameStateService;
    private readonly IGuildService _guildService;
    private readonly IGuildResearchService _researchService;
    private readonly ILocalizationService _localizationService;
    private readonly IGuildChatService _chatService;
    private readonly IGuildWarSeasonService _warSeasonService;
    private readonly IGuildBossService _bossService;
    private readonly IGuildHallService _hallService;
    private readonly IGuildTipService _tipService;
    private readonly IGuildAchievementService _achievementService;
    private bool _isBusy;
    private DateTime _lastChatSend = DateTime.MinValue;
    private Avalonia.Threading.DispatcherTimer? _chatPollTimer;
    private EventHandler? _chatPollHandler;
    private readonly Action<string> _warSeasonNavHandler;
    private readonly Action<string> _bossNavHandler;
    private readonly Action<string> _hallNavHandler;
    private readonly Action<string, string> _warSeasonMsgHandler;
    private readonly Action<string, string> _bossMsgHandler;
    private readonly Action<string, string> _hallMsgHandler;
    private readonly Action _bossCelebrationHandler;
    private readonly Action _hallCelebrationHandler;
    private readonly EventHandler<(string Text, string Type)> _hallFloatingTextHandler;

    // ═══════════════════════════════════════════════════════════════════════
    // EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    public event Action<string>? NavigationRequested;
    public event Action<string, string>? MessageRequested;
    public event Action<string, string>? FloatingTextRequested;
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
    [NotifyPropertyChangedFor(nameof(IsWarSeasonState))]
    [NotifyPropertyChangedFor(nameof(IsBossState))]
    [NotifyPropertyChangedFor(nameof(IsHallState))]
    [NotifyPropertyChangedFor(nameof(IsAchievementsState))]
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

    /// <summary>Kriegs-Saison-Übersicht sichtbar.</summary>
    public bool IsWarSeasonState => ViewState == GuildViewState.WarSeason;

    /// <summary>Boss-Ansicht sichtbar.</summary>
    public bool IsBossState => ViewState == GuildViewState.Boss;

    /// <summary>Hauptquartier-Ansicht sichtbar.</summary>
    public bool IsHallState => ViewState == GuildViewState.Hall;

    /// <summary>Achievements-Ansicht sichtbar.</summary>
    public bool IsAchievementsState => ViewState == GuildViewState.Achievements;

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

    [ObservableProperty]
    private bool _isInviteCodeInputVisible;

    [ObservableProperty]
    private ObservableCollection<GuildInvitationDisplay> _receivedInvites = [];

    [ObservableProperty]
    private bool _hasReceivedInvites;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InvitationsHeaderDisplay))]
    private int _receivedInviteCount;

    /// <summary>Lokalisierter Header-Text für Einladungs-Inbox.</summary>
    public string InvitationsHeaderDisplay =>
        string.Format(_localizationService.GetString("InvitationsHeader") ?? "Einladungen ({0})", ReceivedInviteCount);

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

    [ObservableProperty]
    private bool _hasActiveResearch;

    [ObservableProperty]
    private string _activeResearchName = "";

    [ObservableProperty]
    private string _activeResearchCountdown = "";

    [ObservableProperty]
    private string _activeResearchId = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Chat
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<ChatMessageDisplay> _chatMessages = [];

    [ObservableProperty]
    private string _chatInput = "";

    [ObservableProperty]
    private bool _canSendChat = true;

    [ObservableProperty]
    private string _chatSubtitle = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Gildenkrieg
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private GuildWarDisplayData? _activeWar;

    [ObservableProperty]
    private bool _hasActiveWar;

    [ObservableProperty]
    private string _warStatusText = "";

    [ObservableProperty]
    private string _warTimeRemaining = "";

    [ObservableProperty]
    private string _warSubtitle = "";

    [ObservableProperty]
    private string _warContributionDisplay = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Quick-Status (Hub-Übersicht)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _warQuickStatus = "";

    [ObservableProperty]
    private string _bossQuickStatus = "";

    [ObservableProperty]
    private string _leagueQuickStatus = "";

    [ObservableProperty]
    private string _researchQuickStatus = "";

    [ObservableProperty]
    private string _hallQuickStatus = "";

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Gilden-Achievements
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<GuildAchievementDisplay> _guildAchievements = [];

    [ObservableProperty]
    private bool _hasGuildAchievements;

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES - Kontextuelle Tipps
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _hasActiveTip;

    [ObservableProperty]
    private string _activeTipText = "";

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

    /// <summary>Sub-ViewModel für Gilden-Krieg (Saison-System).</summary>
    public GuildWarSeasonViewModel WarSeasonViewModel { get; }

    /// <summary>Sub-ViewModel für kooperative Bosse.</summary>
    public GuildBossViewModel BossViewModel { get; }

    /// <summary>Sub-ViewModel für das Gilden-Hauptquartier.</summary>
    public GuildHallViewModel HallViewModel { get; }

    public GuildViewModel(
        IGameStateService gameStateService,
        IGuildService guildService,
        IGuildResearchService researchService,
        ILocalizationService localizationService,
        IGuildChatService chatService,
        IGuildWarSeasonService warSeasonService,
        IGuildBossService bossService,
        IGuildHallService hallService,
        IGuildTipService tipService,
        IGuildAchievementService achievementService,
        GuildWarSeasonViewModel warSeasonViewModel,
        GuildBossViewModel bossViewModel,
        GuildHallViewModel hallViewModel)
    {
        _gameStateService = gameStateService;
        _guildService = guildService;
        _researchService = researchService;
        _localizationService = localizationService;
        _chatService = chatService;
        _warSeasonService = warSeasonService;
        _bossService = bossService;
        _hallService = hallService;
        _tipService = tipService;
        _achievementService = achievementService;

        WarSeasonViewModel = warSeasonViewModel;
        BossViewModel = bossViewModel;
        HallViewModel = hallViewModel;

        // Sub-VM Navigation-Events an GuildViewModel weiterleiten (benannte Felder für Unsubscribe)
        _warSeasonNavHandler = route => NavigationRequested?.Invoke(route);
        _bossNavHandler = route => NavigationRequested?.Invoke(route);
        _hallNavHandler = route => NavigationRequested?.Invoke(route);
        WarSeasonViewModel.NavigationRequested += _warSeasonNavHandler;
        BossViewModel.NavigationRequested += _bossNavHandler;
        HallViewModel.NavigationRequested += _hallNavHandler;

        // Sub-VM Message/Celebration/FloatingText-Events weiterleiten
        _warSeasonMsgHandler = (t, m) => MessageRequested?.Invoke(t, m);
        _bossMsgHandler = (t, m) => MessageRequested?.Invoke(t, m);
        _hallMsgHandler = (t, m) => MessageRequested?.Invoke(t, m);
        _bossCelebrationHandler = () => CelebrationRequested?.Invoke();
        _hallCelebrationHandler = () => CelebrationRequested?.Invoke();
        _hallFloatingTextHandler = (s, e) => FloatingTextRequested?.Invoke(e.Text, e.Type);
        WarSeasonViewModel.MessageRequested += _warSeasonMsgHandler;
        BossViewModel.MessageRequested += _bossMsgHandler;
        BossViewModel.CelebrationRequested += _bossCelebrationHandler;
        HallViewModel.MessageRequested += _hallMsgHandler;
        HallViewModel.CelebrationRequested += _hallCelebrationHandler;
        HallViewModel.FloatingTextRequested += _hallFloatingTextHandler;

        _guildService.GuildUpdated += OnGuildUpdated;
        _achievementService.AchievementCompleted += OnGuildAchievementCompleted;

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
                // Effekt-Caches parallel laden (sonst 0 bis Sub-Seite geöffnet wird)
                // SafeFireAndForget loggt Netzwerkfehler statt sie still zu verschlucken
                _researchService.RefreshResearchCacheAsync().SafeFireAndForget();
                _hallService.RefreshHallCacheAsync().SafeFireAndForget();
                // War-Saison initialisieren (lädt aktive War-ID, cached War, Liga)
                _warSeasonService.InitializeAsync().SafeFireAndForget();

                UpdateContributionDisplay();
                await RefreshGuildDetailsAsync();
                ViewState = GuildViewState.InGuild;
            }
            else
            {
                // Spieler als verfügbar registrieren (für Einladungs-Browser)
                await _guildService.RegisterAsAvailableAsync();

                // Einladungs-Inbox laden
                await LoadReceivedInvitesAsync();

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
    // COMMANDS - Einladungs-Code-Eingabe (Browse-State)
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ToggleInviteCodeInput()
    {
        IsInviteCodeInputVisible = !IsInviteCodeInputVisible;
        if (IsInviteCodeInputVisible)
            JoinCodeInput = "";
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
        if (research == null || research.IsCompleted || research.IsResearching || !research.IsActive) return;

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

            var success = await _researchService.ContributeToResearchAsync(SelectedResearchId, amount);
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

    [RelayCommand]
    private void NavigateToChat() => NavigationRequested?.Invoke("guild_chat");

    [RelayCommand]
    private void NavigateToWar() => NavigationRequested?.Invoke("guild_war");

    [RelayCommand]
    private void NavigateToWarSeason() => NavigationRequested?.Invoke("guild_war_season");

    [RelayCommand]
    private void NavigateToBoss() => NavigationRequested?.Invoke("guild_boss");

    [RelayCommand]
    private void NavigateToHall() => NavigationRequested?.Invoke("guild_hall");

    [RelayCommand]
    private void NavigateToAchievements() => NavigationRequested?.Invoke("guild_achievements");

    [RelayCommand]
    private void DismissTip()
    {
        HasActiveTip = false;
        _tipService.MarkTipSeen("guild_hub");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Einladungs-System
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private void ShareInviteCode()
    {
        if (string.IsNullOrEmpty(GuildInviteCode)) return;

        var shareText = $"{(_localizationService.GetString("InviteCode") ?? "Einladungs-Code")}: {GuildInviteCode}";
        UriLauncher.ShareText(shareText, _localizationService.GetString("ShareCode") ?? "Code teilen");

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
                    LevelDisplay = $"Lv.{p.Level}",
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
            // Direkte Einladung an den Spieler senden (Firebase-basiert)
            var success = await _guildService.SendInviteAsync(player.Uid);
            if (success)
            {
                player.IsInvited = true;
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("InviteSent") ?? "Einladung gesendet");
            }
            else
            {
                MessageRequested?.Invoke(
                    _localizationService.GetString("Guild") ?? "Innung",
                    _localizationService.GetString("InviteFailed") ?? "Einladung fehlgeschlagen");
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Einladungs-Inbox
    // ═══════════════════════════════════════════════════════════════════════

    private async Task LoadReceivedInvitesAsync()
    {
        try
        {
            var invites = await _guildService.GetReceivedInvitesAsync();
            var maxMembers = _guildService.GetMaxMembers();
            var displays = new ObservableCollection<GuildInvitationDisplay>();
            foreach (var (guildId, invite) in invites)
            {
                displays.Add(new GuildInvitationDisplay
                {
                    GuildId = guildId,
                    GuildName = invite.GuildName,
                    GuildIcon = invite.GuildIcon,
                    GuildColor = invite.GuildColor,
                    GuildLevel = invite.GuildLevel,
                    MemberDisplay = $"{invite.MemberCount}/{maxMembers}",
                    InvitedByDisplay = $"{_localizationService.GetString("InvitedByPrefix") ?? "Eingeladen von:"} {invite.InvitedBy}"
                });
            }
            ReceivedInvites = displays;
            HasReceivedInvites = displays.Count > 0;
            ReceivedInviteCount = displays.Count;
        }
        catch
        {
            ReceivedInvites = [];
            HasReceivedInvites = false;
            ReceivedInviteCount = 0;
        }
    }

    [RelayCommand]
    private async Task AcceptInviteAsync(GuildInvitationDisplay? invite)
    {
        if (invite == null || _isBusy) return;
        _isBusy = true;
        try
        {
            var success = await _guildService.AcceptInviteAsync(invite.GuildId);
            if (success)
            {
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
                    _localizationService.GetString("GuildFull") ?? "Gilde ist voll");
            }
        }
        finally
        {
            _isBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeclineInviteAsync(GuildInvitationDisplay? invite)
    {
        if (invite == null || _isBusy) return;
        _isBusy = true;
        try
        {
            await _guildService.DeclineInviteAsync(invite.GuildId);
            ReceivedInvites.Remove(invite);
            HasReceivedInvites = ReceivedInvites.Count > 0;
            ReceivedInviteCount = ReceivedInvites.Count;
        }
        finally
        {
            _isBusy = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Chat
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task SendChatMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(ChatInput) || ChatInput.Length > 200) return;
        if (!CanSendChat) return;
        if ((DateTime.UtcNow - _lastChatSend).TotalSeconds < 5) return;

        var membership = _gameStateService.State.GuildMembership;
        if (membership == null) return;

        CanSendChat = false;
        try
        {
            var success = await _chatService.SendMessageAsync(membership.GuildId, ChatInput.Trim());
            if (success)
            {
                ChatInput = "";
                _lastChatSend = DateTime.UtcNow;
                await LoadChatMessagesAsync();
                // Cooldown 5 Sekunden nur bei Erfolg
                _ = Task.Delay(5000).ContinueWith(_ =>
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => CanSendChat = true));
            }
            else
            {
                CanSendChat = true;
            }
        }
        catch
        {
            CanSendChat = true;
        }
    }

    /// <summary>
    /// Startet den 15-Sekunden Polling-Timer für den Chat.
    /// </summary>
    public void StartChatPolling()
    {
        StopChatPolling();
        _chatPollTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _chatPollHandler = async (_, _) => await LoadChatMessagesAsync();
        _chatPollTimer.Tick += _chatPollHandler;
        _chatPollTimer.Start();
    }

    /// <summary>
    /// Stoppt den Chat-Polling-Timer und entfernt den Event-Handler.
    /// </summary>
    public void StopChatPolling()
    {
        if (_chatPollTimer != null && _chatPollHandler != null)
            _chatPollTimer.Tick -= _chatPollHandler;
        _chatPollTimer?.Stop();
        _chatPollHandler = null;
        _chatPollTimer = null;
    }

    /// <summary>
    /// Laedt die letzten 50 Chat-Nachrichten der Gilde.
    /// Diff-basiert: Nur neue Nachrichten werden angehängt, alte getrimmt.
    /// Vermeidet kompletten UI-Rebuild bei jedem Polling-Zyklus.
    /// </summary>
    public async Task LoadChatMessagesAsync()
    {
        var membership = _gameStateService.State.GuildMembership;
        if (membership == null) return;

        try
        {
            var messages = await _chatService.GetRecentMessagesAsync(membership.GuildId);
            var latest = messages.TakeLast(50).ToList();

            if (ChatMessages.Count == 0)
            {
                // Erstbefüllung: Komplett initialisieren
                ChatMessages = new ObservableCollection<ChatMessageDisplay>(latest);
            }
            else
            {
                // Diff-Update: Nur neue Nachrichten anhängen (Vergleich per Timestamp + Uid)
                var lastKnownTimestamp = ChatMessages[^1].Timestamp;
                var lastKnownUid = ChatMessages[^1].Uid;

                var newMessages = new List<ChatMessageDisplay>();
                var foundLast = false;
                foreach (var msg in latest)
                {
                    if (foundLast)
                    {
                        newMessages.Add(msg);
                    }
                    else if (msg.Timestamp == lastKnownTimestamp && msg.Uid == lastKnownUid)
                    {
                        foundLast = true;
                    }
                }

                // Neue Nachrichten anhängen
                foreach (var msg in newMessages)
                    ChatMessages.Add(msg);

                // Alte Nachrichten am Anfang trimmen wenn > 50
                while (ChatMessages.Count > 50)
                    ChatMessages.RemoveAt(0);
            }

            ChatSubtitle = latest.Count > 0
                ? latest[^1].Text
                : (_localizationService.GetString("NoChatMessages") ?? "Noch keine Nachrichten");
        }
        catch
        {
            ChatSubtitle = _localizationService.GetString("NoChatMessages") ?? "Noch keine Nachrichten";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMANDS - Gildenkrieg
    // ═══════════════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task ContributeToWarAsync()
    {
        if (ActiveWar == null || !ActiveWar.IsActive) return;
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            // Beitrag: 10% des aktuellen Spieler-Levels als Score-Punkte
            var level = _gameStateService.State.PlayerLevel;
            var points = Math.Max(1, level * 10);
            await _warSeasonService.ContributeScoreAsync(points, "manual");
            await LoadWarStatusAsync();

            MessageRequested?.Invoke(
                _localizationService.GetString("GuildWarTitle") ?? "Gildenkrieg",
                string.Format(
                    _localizationService.GetString("WarContribute") ?? "+{0} Punkte beigetragen!",
                    points));
        }
        finally
        {
            _isBusy = false;
        }
    }

    /// <summary>
    /// Laedt den aktuellen Kriegs-Status.
    /// </summary>
    public async Task LoadWarStatusAsync()
    {
        try
        {
            var seasonData = await _warSeasonService.GetCurrentWarDataAsync();
            var isActive = seasonData != null && !string.IsNullOrEmpty(seasonData.WarId)
                           && !string.IsNullOrEmpty(seasonData.OpponentName);

            // GuildWarDisplayData für die einfache GuildWarView befüllen
            var membership = _gameStateService.State.GuildMembership;
            ActiveWar = seasonData != null ? new GuildWarDisplayData
            {
                OwnGuildName = membership?.GuildName ?? "",
                OpponentGuildName = seasonData.OpponentName ?? "",
                OwnScore = seasonData.OwnScore,
                OpponentScore = seasonData.OpponentScore,
                EndDate = DateTime.TryParse(seasonData.PhaseEndsAt, CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var ed) ? ed : DateTime.UtcNow,
                IsActive = isActive
            } : null;

            HasActiveWar = isActive;

            if (isActive && seasonData != null)
            {
                var remaining = ActiveWar!.EndDate - DateTime.UtcNow;
                WarTimeRemaining = remaining > TimeSpan.Zero
                    ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}min"
                    : (_localizationService.GetString("WarResult") ?? "Ergebnis steht fest");
                WarStatusText = $"{MoneyFormatter.Format(seasonData.OwnScore, 0)} vs {MoneyFormatter.Format(seasonData.OpponentScore, 0)}";
                WarSubtitle = WarStatusText;
            }
            else
            {
                HasActiveWar = false;
                WarSubtitle = _localizationService.GetString("NoActiveWar") ?? "Kein aktiver Krieg";
            }
        }
        catch
        {
            HasActiveWar = false;
            WarSubtitle = _localizationService.GetString("NoActiveWar") ?? "Kein aktiver Krieg";
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
        LoadGuildDataCommand.ExecuteAsync(null).SafeFireAndForget();
        // Quick-Status für Hub parallel laden
        RefreshQuickStatusAsync().SafeFireAndForget();
    }

    /// <summary>
    /// Lokalisierte Texte aktualisieren.
    /// </summary>
    public void UpdateLocalizedTexts()
    {
        Title = _localizationService.GetString("Guild") ?? "Innung";
        ChatSubtitle = _localizationService.GetString("NoChatMessages") ?? "Noch keine Nachrichten";
        WarSubtitle = _localizationService.GetString("NoActiveWar") ?? "Kein aktiver Krieg";
        WarSeasonViewModel.UpdateLocalizedTexts();
        BossViewModel.UpdateLocalizedTexts();
        HallViewModel.UpdateLocalizedTexts();
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

        // Chat + War laden
        await LoadChatMessagesAsync();
        await LoadWarStatusAsync();

        // Einladungs-Code laden (für Invite-Seite)
        var code = await _guildService.GetOrCreateInviteCodeAsync();
        GuildInviteCode = code ?? "";
    }

    private async Task LoadGuildResearchAsync()
    {
        var items = await _researchService.GetGuildResearchAsync();

        // Namen und Beschreibungen lokalisieren
        foreach (var item in items)
        {
            item.Name = _localizationService.GetString(item.Name) ?? item.Name;
            item.Description = _localizationService.GetString(item.Description) ?? item.Description;
        }

        // RemainingTime für forschende Items berechnen (für SkiaSharp-Fortschrittsring)
        var effects = _researchService.GetCachedEffects();
        foreach (var item in items.Where(r => r.IsResearching && !string.IsNullOrEmpty(r.ResearchStartedAt)))
        {
            if (DateTime.TryParse(item.ResearchStartedAt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var startedAt))
            {
                var durH = item.DurationHours;
                if (effects.ResearchSpeedBonus > 0)
                    durH *= (double)(1m - effects.ResearchSpeedBonus);
                var endTime = startedAt.AddHours(durH);
                var remaining = endTime - DateTime.UtcNow;
                item.RemainingTime = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        GuildResearch = new ObservableCollection<GuildResearchDisplay>(items);
        HasGuildResearch = GuildResearch.Count > 0;

        // Laufende Forschung finden
        var researching = items.FirstOrDefault(r => r.IsResearching);
        if (researching != null)
        {
            HasActiveResearch = true;
            ActiveResearchName = researching.Name;
            ActiveResearchId = researching.Id;
            UpdateResearchCountdown(researching);
        }
        else
        {
            HasActiveResearch = false;
            ActiveResearchName = "";
            ActiveResearchCountdown = "";
            ActiveResearchId = "";
        }

        var completed = items.Count(i => i.IsCompleted);
        GuildResearchSummary = $"{completed}/{items.Count}";

        var maxMembers = _guildService.GetMaxMembers();
        MaxMembersDisplay = $"Max. {maxMembers}";
    }

    /// <summary>
    /// Aktualisiert die Countdown-Anzeige für die laufende Forschung.
    /// </summary>
    private void UpdateResearchCountdown(GuildResearchDisplay research)
    {
        if (string.IsNullOrEmpty(research.ResearchStartedAt)) return;

        if (!DateTime.TryParse(research.ResearchStartedAt,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var startedAt))
            return;

        var durationHours = research.DurationHours;
        // Schnellforschung-Bonus
        var effects = _researchService.GetCachedEffects();
        if (effects.ResearchSpeedBonus > 0)
            durationHours *= (double)(1m - effects.ResearchSpeedBonus);

        var endTime = startedAt.AddHours(durationHours);
        var remaining = endTime - DateTime.UtcNow;

        // RemainingTime im Display-Objekt aktualisieren (für SkiaSharp-Renderer)
        research.RemainingTime = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;

        if (remaining <= TimeSpan.Zero)
        {
            ActiveResearchCountdown = _localizationService.GetString("GuildResearchDone") ?? "Fertig!";
        }
        else
        {
            // Lokalisiertes Format: "Noch 3h 24min" / "3h 24min remaining"
            var timeStr = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}min"
                : $"{remaining.Minutes}min {remaining.Seconds:D2}s";
            var template = _localizationService.GetString("GuildResearchTimeRemaining") ?? "{0}";
            ActiveResearchCountdown = string.Format(template, timeStr);
        }
    }

    /// <summary>
    /// Öffentlicher Aufruf zur Countdown-Aktualisierung (für periodischen Refresh aus der View).
    /// </summary>
    public void RefreshActiveResearchCountdown()
    {
        if (!HasActiveResearch) return;

        var researching = GuildResearch.FirstOrDefault(r => r.IsResearching);
        if (researching != null)
        {
            UpdateResearchCountdown(researching);

            // Timer abgelaufen → automatisch abschließen (einmalig)
            if (researching.RemainingTime.HasValue && researching.RemainingTime.Value <= TimeSpan.Zero)
            {
                HasActiveResearch = false; // Guard: verhindert mehrfachen Aufruf bis Reload
                CompleteResearchTimerAsync().SafeFireAndForget();
            }
        }
    }

    /// <summary>
    /// Schließt eine abgelaufene Forschung in Firebase ab und lädt Forschungsdaten neu.
    /// </summary>
    private async Task CompleteResearchTimerAsync()
    {
        try
        {
            var completed = await _researchService.CheckResearchCompletionAsync();
            if (completed)
            {
                CelebrationRequested?.Invoke();
            }
            // Forschungsdaten immer neu laden (auch wenn kein Completion - Daten könnten veraltet sein)
            await LoadGuildResearchAsync();
        }
        catch
        {
            // Bei Fehler: HasActiveResearch wird durch LoadGuildResearchAsync korrekt gesetzt
        }
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopChatPolling();
        _guildService.GuildUpdated -= OnGuildUpdated;
        _achievementService.AchievementCompleted -= OnGuildAchievementCompleted;

        // Navigation-Events
        WarSeasonViewModel.NavigationRequested -= _warSeasonNavHandler;
        BossViewModel.NavigationRequested -= _bossNavHandler;
        HallViewModel.NavigationRequested -= _hallNavHandler;

        // Message/Celebration/FloatingText-Events
        WarSeasonViewModel.MessageRequested -= _warSeasonMsgHandler;
        BossViewModel.MessageRequested -= _bossMsgHandler;
        BossViewModel.CelebrationRequested -= _bossCelebrationHandler;
        HallViewModel.MessageRequested -= _hallMsgHandler;
        HallViewModel.CelebrationRequested -= _hallCelebrationHandler;
        HallViewModel.FloatingTextRequested -= _hallFloatingTextHandler;
    }

    /// <summary>
    /// Aktualisiert die Quick-Status-Anzeigen im Guild-Hub.
    /// Wird nach dem Laden der Gilden-Daten aufgerufen.
    /// </summary>
    public async Task RefreshQuickStatusAsync()
    {
        try
        {
            // War Quick-Status
            WarQuickStatus = WarSeasonViewModel.GetQuickStatus();

            // Boss Quick-Status
            BossQuickStatus = BossViewModel.GetQuickStatus();

            // Liga Quick-Status
            var league = _warSeasonService.GetCurrentLeague();
            LeagueQuickStatus = league switch
            {
                GuildLeague.Bronze => _localizationService.GetString("LeagueBronze") ?? "Bronze",
                GuildLeague.Silver => _localizationService.GetString("LeagueSilver") ?? "Silber",
                GuildLeague.Gold => _localizationService.GetString("LeagueGold") ?? "Gold",
                GuildLeague.Diamond => _localizationService.GetString("LeagueDiamond") ?? "Diamant",
                _ => "Bronze"
            };

            // Research Quick-Status
            var research = await _researchService.GetGuildResearchAsync();
            var completed = research.Count(r => r.IsCompleted);
            ResearchQuickStatus = $"{completed}/{research.Count}";

            // Hall Quick-Status
            HallQuickStatus = HallViewModel.GetQuickStatus();

            // Tipp prüfen
            var tip = _tipService.GetTipForContext("guild_hub");
            HasActiveTip = tip != null;
            ActiveTipText = tip ?? "";
        }
        catch
        {
            // Quick-Status-Fehler sind nicht kritisch
        }
    }

    /// <summary>
    /// Lädt die Gilden-Achievements (für die Achievements-Sub-Seite).
    /// </summary>
    public async Task LoadGuildAchievementsAsync()
    {
        try
        {
            var achievements = await _achievementService.GetAchievementsAsync();
            // RESX-Keys in lokalisierte Texte auflösen
            foreach (var a in achievements)
            {
                a.Name = _localizationService.GetString(a.Name) ?? a.Name;
                a.Description = _localizationService.GetString(a.Description) ?? a.Description;
            }
            GuildAchievements = new ObservableCollection<GuildAchievementDisplay>(achievements);
            HasGuildAchievements = GuildAchievements.Count > 0;
        }
        catch
        {
            // Fehler beim Laden der Achievements ist nicht kritisch
        }
    }

    private void OnGuildAchievementCompleted(GuildAchievementDisplay achievement)
    {
        CelebrationRequested?.Invoke();
        MessageRequested?.Invoke(
            _localizationService.GetString("GuildAchievementUnlocked") ?? "Gilden-Erfolg!",
            $"{achievement.Name} (+{achievement.GoldenScrewReward} GS)");
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
public partial class AvailablePlayerDisplay : ObservableObject
{
    public string Uid { get; set; } = "";
    public string Name { get; set; } = "";
    public string LevelDisplay { get; set; } = "";
    public string LastActiveDisplay { get; set; } = "";

    [ObservableProperty]
    private bool _isInvited;

    public string InviteButtonText { get; set; } = "Einladen";
    public string InvitedText { get; set; } = "Eingeladen";
}
