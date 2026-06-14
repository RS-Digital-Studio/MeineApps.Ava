using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Services.Interfaces;
using HandwerkerImperium.ViewModels.Guild;
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
/// Sub-Tabs innerhalb des InGuild-States. Ersetzt die 7er-Scroll-Liste durch
/// eine Bottom-Tab-Leiste (v2.0.34, UX-Refactor).
/// </summary>
public enum GuildSubTab
{
    /// <summary>Wochen-Ziel, Contribute, aktive Tipps, Quick-Info.</summary>
    Overview,
    /// <summary>Krieg und Boss (beide PvP-Elemente zusammengefasst).</summary>
    Combat,
    /// <summary>Gilden-Forschung.</summary>
    Research,
    /// <summary>Gilden-Chat.</summary>
    Chat,
    /// <summary>Mitglieder-Liste + Hauptquartier + Erfolge.</summary>
    Members
}

/// <summary>
/// ViewModel für das Multiplayer-Gildensystem via Firebase.
/// Sechs UI-Zustände via GuildViewState Enum (flache Panels, keine verschachtelte IsVisible-Logik).
/// </summary>
/// <remarks>
/// Partial-Split (v2.1.4) — kohärente UI-Zustandsmaschine, aufgeteilt nach Sub-Bereichen:
/// <list type="bullet">
/// <item>GuildViewModel.cs — Felder, Konstruktor, Events, ViewState/SubTab, Dispose, gemeinsame Refresh-Helfer.</item>
/// <item>GuildViewModel.Membership.cs — Laden, Erstellen, Beitreten, Verlassen, Beitragen.</item>
/// <item>GuildViewModel.Invites.cs — Invite-Code, Spieler-Browser, Einladungs-Inbox.</item>
/// <item>GuildViewModel.Research.cs — Gilden-Forschung, Countdown, Auto-Abschluss.</item>
/// <item>GuildViewModel.Chat.cs — Chat, Polling-Timer, Diff-Laden.</item>
/// <item>GuildViewModel.Combat.cs — Gildenkrieg, Quick-Status, Achievements, Sub-Seiten-Navigation.</item>
/// </list>
/// </remarks>
public sealed partial class GuildViewModel : ViewModelBase, INavigable, IDisposable
{
    private bool _disposed;
    private readonly IGameStateService _gameStateService;
    private readonly IGuildFacade _facade;
    private readonly ILocalizationService _localizationService;
    private readonly IDialogService _dialogService;
    private readonly IContextualHintService? _contextualHintService;
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
    // PROPERTIES - Sub-Tab im InGuild-State (v2.0.34 UX-Refactor)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Aktiver Sub-Tab innerhalb des InGuild-States.
    /// Ersetzt die 7er-Scroll-Liste durch 5 horizontale Tabs (Overview/Combat/Research/Chat/Members).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverviewTabActive))]
    [NotifyPropertyChangedFor(nameof(IsCombatTabActive))]
    [NotifyPropertyChangedFor(nameof(IsResearchTabActive))]
    [NotifyPropertyChangedFor(nameof(IsChatTabActive))]
    [NotifyPropertyChangedFor(nameof(IsMembersTabActive))]
    private GuildSubTab _activeSubTab = GuildSubTab.Overview;

    public bool IsOverviewTabActive => ActiveSubTab == GuildSubTab.Overview;
    public bool IsCombatTabActive => ActiveSubTab == GuildSubTab.Combat;
    public bool IsResearchTabActive => ActiveSubTab == GuildSubTab.Research;
    public bool IsChatTabActive => ActiveSubTab == GuildSubTab.Chat;
    public bool IsMembersTabActive => ActiveSubTab == GuildSubTab.Members;

    /// <summary>
    /// Selektiert einen Sub-Tab (Parameter: "Overview", "Combat", "Research", "Chat", "Members").
    /// </summary>
    [RelayCommand]
    private void SelectSubTab(string tabName)
    {
        if (Enum.TryParse<GuildSubTab>(tabName, ignoreCase: true, out var tab))
            ActiveSubTab = tab;
    }

    /// <summary>
    /// Polling fuer Co-op + Auktion startet/stoppt mit dem Combat-Tab (v2.1.0).
    /// Spart Firebase-Requests + Battery wenn der Tab nicht offen ist.
    /// </summary>
    partial void OnActiveSubTabChanged(GuildSubTab value)
    {
        if (value == GuildSubTab.Combat)
        {
            CoopOrderVM.StartPolling();
            AuctionVM.StartPolling();
        }
        else
        {
            CoopOrderVM.StopPolling();
            AuctionVM.StopPolling();
        }
    }

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

    /// <summary>
    /// GUILD-10: Zeigt gecachte Gilden-Boni im Offline-State an.
    /// Format: "Gilden-Boni aktiv (offline)" + Details der gecachten Effekte.
    /// </summary>
    [ObservableProperty]
    private string _cachedBonusInfo = "";

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

    // Thin-Wrapper-Sub-VMs fuer ViewLocator-Mapping der 6 Sub-Seiten ( 17.04.2026)
    public GuildResearchViewModel ResearchVM { get; private set; } = null!;
    public GuildMembersViewModel MembersVM { get; private set; } = null!;
    public GuildInviteViewModel InviteVM { get; private set; } = null!;
    public GuildAchievementsViewModel AchievementsVM { get; private set; } = null!;
    public GuildChatViewModel ChatVM { get; private set; } = null!;
    public GuildWarViewModel WarVM { get; private set; } = null!;

    /// <summary>Co-op-Auftraege ViewModel (v2.1.0). Polling laeuft beim Combat-Tab.</summary>
    public GuildCoopOrderViewModel CoopOrderVM { get; }

    /// <summary>Worker-Auktion ViewModel (v2.1.0). Polling laeuft beim Combat-Tab.</summary>
    public ViewModels.Auctions.WorkerAuctionViewModel AuctionVM { get; }

    /// <summary>V7 (, Plan Section 3.9): Mega-Projekt-ViewModel.</summary>
    public GuildMegaProjectViewModel MegaProjectVM { get; }

    public GuildViewModel(
        IGameStateService gameStateService,
        IGuildFacade facade,
        ILocalizationService localizationService,
        IDialogService dialogService,
        GuildWarSeasonViewModel warSeasonViewModel,
        GuildBossViewModel bossViewModel,
        GuildHallViewModel hallViewModel,
        GuildCoopOrderViewModel coopOrderViewModel,
        ViewModels.Auctions.WorkerAuctionViewModel auctionViewModel,
        GuildMegaProjectViewModel megaProjectViewModel,
        IContextualHintService? contextualHintService = null)
    {
        _gameStateService = gameStateService;
        _facade = facade;
        _localizationService = localizationService;
        _dialogService = dialogService;
        _contextualHintService = contextualHintService;

        WarSeasonViewModel = warSeasonViewModel;
        BossViewModel = bossViewModel;
        HallViewModel = hallViewModel;
        CoopOrderVM = coopOrderViewModel;
        AuctionVM = auctionViewModel;
        MegaProjectVM = megaProjectViewModel;

        // v2.1.0: Co-op-Picker-Provider verdrahten — liefert alle Gildenmitglieder ausser dem Spieler selbst.
        // Member-Liste wird vom GuildViewModel verwaltet (RefreshGuildDetails) — Provider wird zur
        // Click-Zeit (OpenPicker) ausgewertet damit immer die aktuelle Liste verwendet wird.
        CoopOrderVM.AvailableMembersProvider = () =>
        {
            var list = new List<(string PlayerId, string Name)>();
            foreach (var m in Members)
            {
                if (m.IsPlayer) continue;
                if (string.IsNullOrEmpty(m.PlayerId)) continue;
                list.Add((m.PlayerId, m.Name));
            }
            return list;
        };

        // v2.1.0: Co-op-NavigationRequested an Parent weiterleiten (MainViewModel routet zum MiniGame).
        CoopOrderVM.NavigationRequested += route => NavigationRequested?.Invoke(route);

        // Thin-Wrapper-Sub-VMs fuer ViewLocator ( 17.04.2026)
        ResearchVM = new GuildResearchViewModel(this);
        MembersVM = new GuildMembersViewModel(this);
        InviteVM = new GuildInviteViewModel(this);
        AchievementsVM = new GuildAchievementsViewModel(this);
        ChatVM = new GuildChatViewModel(this);
        WarVM = new GuildWarViewModel(this);

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

        _facade.Guild.GuildUpdated += OnGuildUpdated;
        _facade.Achievement.AchievementCompleted += OnGuildAchievementCompleted;

        UpdateLocalizedTexts();
        RefreshFromLocalState();
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
        Title = _localizationService.GetString("Guild") ?? "Guild";
        ChatSubtitle = _localizationService.GetString("NoChatMessages") ?? "No messages";
        WarSubtitle = _localizationService.GetString("NoActiveWar") ?? "No active war";
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
            IncomeBonusDetailDisplay = $"+{bonus * 100:F0}% {(_localizationService.GetString("GuildIncomeBonus") ?? "Income Bonus")}";
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

    private void OnGuildUpdated()
    {
        RefreshFromLocalState();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopChatPolling();
        _facade.Guild.GuildUpdated -= OnGuildUpdated;
        _facade.Achievement.AchievementCompleted -= OnGuildAchievementCompleted;

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

        // Co-op + Auktion Polling stoppen (v2.1.0)
        CoopOrderVM.StopPolling();
        AuctionVM.StopPolling();
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

    /// <summary>v2.1.0: PlayerId fuer Co-op-Auftrags-Einladungen + Direct-Messages.</summary>
    public string PlayerId { get; set; } = "";
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
