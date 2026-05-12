using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandwerkerImperium.Helpers;
using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.Services;
using HandwerkerImperium.ViewModels.MiniGames;

namespace HandwerkerImperium.ViewModels;


/// <summary>
/// MainViewModel Tab-State (AAA-Audit Review-Pass 12.05.2026): ActivePage-Enum +
/// IsXxxActive-Computed-Properties + Imperium-Sub-Tabs + Progressive-Tab-Unlock-Logic.
/// Aus MainViewModel.Properties.cs extrahiert um Themen-Cluster zu trennen.
/// </summary>
public sealed partial class MainViewModel
{
    // ZENTRALES NAVIGATION-STATE (ActivePage Enum statt 35+ Booleans)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    private ActivePage _activePage = ActivePage.Dashboard;

    /// <summary>
    /// Feuert VOR dem ActivePage-Wert-Wechsel → View setzt Opacity=0 bevor Bindings updaten.
    /// Verhindert Flimmern (schwarzer Blitz) beim Tab-Wechsel.
    /// </summary>
    partial void OnActivePageChanging(ActivePage value)
    {
        if (value != _activePage)
            PageTransitionStarting?.Invoke();
    }

    /// <summary>
    /// Navigations-History für kontextuelle Rück-Navigation.
    /// Merkt sich die vorherige Seite damit Back immer dorthin zurückkehrt woher man kam.
    /// Max 10 Einträge (reicht für tiefste Verschachtelung).
    /// </summary>
    // Code-Review-Fix [Finding 5]: O(1)-Cap-Handling statt O(n)-Rebuild.
    private readonly Helpers.CappedNavigationStack _navigationStack = new(MaxNavigationStackSize);
    private bool _isNavigatingBack;
    private const int MaxNavigationStackSize = 10;

    /// <summary>
    /// Zentrale Seitenumschaltung mit Seiteneffekten (GuildChat stoppen, PropertyChanged).
    /// Wird von CommunityToolkit automatisch aufgerufen wenn ActivePage sich ändert.
    /// </summary>
    partial void OnActivePageChanged(ActivePage oldValue, ActivePage newValue)
    {
        // P1.5 AAA-Audit: Stack-Management an Helper delegiert (Sprint A — Helper-Variante).
        Helpers.PageNavigationHelper.ManageStack(_navigationStack, oldValue, newValue, _isNavigatingBack);

        // GuildChat-Polling stoppen wenn Chat verlassen wird
        if (oldValue == ActivePage.GuildChat)
            GuildViewModel.StopChatPolling();

        // PropertyChanged für die berechneten IsXxxActive-Properties (nur die 2 geänderten)
        var oldProp = Helpers.PageNavigationHelper.GetPropertyNameFor(oldValue);
        var newProp = Helpers.PageNavigationHelper.GetPropertyNameFor(newValue);
        if (oldProp != null) OnPropertyChanged(oldProp);
        if (newProp != null) OnPropertyChanged(newProp);
        OnPropertyChanged(nameof(IsTabBarVisible));
        OnPropertyChanged(nameof(BreadcrumbText));

        // MiniGame-ContentControl aktualisieren (ein einziges statt 10 separate)
        OnPropertyChanged(nameof(ActiveMiniGameViewModel));
        OnPropertyChanged(nameof(IsAnyMiniGameActive));

        // AAA-Audit P0 Lazy-View-Loading: Zentrales ActivePageContent feuern, damit das
        // einzelne ContentControl in MainView die richtige Sub-View materialisiert.
        OnPropertyChanged(nameof(ActivePageContent));
        OnPropertyChanged(nameof(HasActivePageContent));
    }

    // Berechnete Navigation-Properties (XAML-Bindings bleiben unverändert)
    public bool IsDashboardActive => ActivePage == ActivePage.Dashboard;
    public bool IsShopActive => ActivePage == ActivePage.Shop;
    public bool IsStatisticsActive => ActivePage == ActivePage.Statistics;
    public bool IsAchievementsActive => ActivePage == ActivePage.Achievements;
    public bool IsSettingsActive => ActivePage == ActivePage.Settings;
    public bool IsWorkshopDetailActive => ActivePage == ActivePage.WorkshopDetail;
    public bool IsOrderDetailActive => ActivePage == ActivePage.OrderDetail;
    public bool IsSawingGameActive => ActivePage == ActivePage.SawingGame;
    public bool IsPipePuzzleActive => ActivePage == ActivePage.PipePuzzle;
    public bool IsWiringGameActive => ActivePage == ActivePage.WiringGame;
    public bool IsPaintingGameActive => ActivePage == ActivePage.PaintingGame;
    public bool IsRoofTilingGameActive => ActivePage == ActivePage.RoofTilingGame;
    public bool IsBlueprintGameActive => ActivePage == ActivePage.BlueprintGame;
    public bool IsDesignPuzzleGameActive => ActivePage == ActivePage.DesignPuzzleGame;
    public bool IsInspectionGameActive => ActivePage == ActivePage.InspectionGame;
    public bool IsWorkerMarketActive => ActivePage == ActivePage.WorkerMarket;
    public bool IsBuildingsActive => ActivePage == ActivePage.Buildings;
    public bool IsResearchActive => ActivePage == ActivePage.Research;
    public bool IsManagerActive => ActivePage == ActivePage.Manager;
    public bool IsTournamentActive => ActivePage == ActivePage.Tournament;
    public bool IsSeasonalEventActive => ActivePage == ActivePage.SeasonalEvent;
    public bool IsBattlePassActive => ActivePage == ActivePage.BattlePass;
    public bool IsGuildActive => ActivePage == ActivePage.Guild;
    public bool IsMissionenActive => ActivePage == ActivePage.Missionen;
    public bool IsGuildResearchActive => ActivePage == ActivePage.GuildResearch;
    public bool IsGuildMembersActive => ActivePage == ActivePage.GuildMembers;
    public bool IsGuildInviteActive => ActivePage == ActivePage.GuildInvite;
    public bool IsGuildWarSeasonActive => ActivePage == ActivePage.GuildWarSeason;
    public bool IsGuildBossActive => ActivePage == ActivePage.GuildBoss;
    public bool IsGuildHallActive => ActivePage == ActivePage.GuildHall;
    public bool IsGuildAchievementsActive => ActivePage == ActivePage.GuildAchievements;
    public bool IsGuildChatActive => ActivePage == ActivePage.GuildChat;
    public bool IsGuildWarActive => ActivePage == ActivePage.GuildWar;
    public bool IsCraftingActive => ActivePage == ActivePage.Crafting;
    public bool IsForgeGameActive => ActivePage == ActivePage.ForgeGame;
    public bool IsInventGameActive => ActivePage == ActivePage.InventGame;
    public bool IsAscensionActive => ActivePage == ActivePage.Ascension;
    public bool IsPrestigeActive => ActivePage == ActivePage.Prestige;

    // ═══════════════════════════════════════════════════════════════════════
    // IMPERIUM-SUB-TABS (v2.0.37)
    // ═══════════════════════════════════════════════════════════════════════

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImperiumWorkshopsActive))]
    [NotifyPropertyChangedFor(nameof(IsImperiumWarehouseActive))]
    [NotifyPropertyChangedFor(nameof(IsImperiumWorkersActive))]
    [NotifyPropertyChangedFor(nameof(IsImperiumResearchActive))]
    [NotifyPropertyChangedFor(nameof(IsImperiumEquipmentActive))]
    [NotifyPropertyChangedFor(nameof(IsImperiumAscensionActive))]
    private ImperiumSubTab _imperiumSubTab = ImperiumSubTab.Workshops;

    public bool IsImperiumWorkshopsActive => ImperiumSubTab == ImperiumSubTab.Workshops;
    /// <summary>V7 (Phase 1 Ressourcen-Plan): Lager-Sub-Tab im Imperium.</summary>
    public bool IsImperiumWarehouseActive => ImperiumSubTab == ImperiumSubTab.Warehouse;
    public bool IsImperiumWorkersActive => ImperiumSubTab == ImperiumSubTab.Workers;
    public bool IsImperiumResearchActive => ImperiumSubTab == ImperiumSubTab.Research;
    public bool IsImperiumEquipmentActive => ImperiumSubTab == ImperiumSubTab.Equipment;
    public bool IsImperiumAscensionActive => ImperiumSubTab == ImperiumSubTab.Ascension;

    /// <summary>
    /// V7: Lager-Sub-Tab erst ab Spielerlevel 50 (analog zu Auto-Produktion).
    /// Vorher gibt es nichts zu lagern.
    /// </summary>
    public bool IsImperiumWarehouseUnlocked
        => HeaderVM.PlayerLevel >= GameBalanceConstants.AutoProductionUnlockLevel;

    /// <summary>
    /// Imperium-Sub-Tab waehlen (per RelayCommand aus AXAML).
    /// Ascension-Sub-Tab nur sichtbar wenn Ascension verfuegbar (PrestigeData.LegendeCount &gt;= 3).
    /// </summary>
    [RelayCommand]
    private void SelectImperiumSubTab(string subTabName)
    {
        if (Enum.TryParse<ImperiumSubTab>(subTabName, ignoreCase: true, out var tab))
            ImperiumSubTab = tab;
    }

    /// <summary>True wenn Ascension-Sub-Tab freigeschaltet (3x Legende-Prestige).</summary>
    public bool IsImperiumAscensionUnlocked
        => _gameStateService.Prestige.LegendeCount >= 3;

    /// <summary>
    /// Gibt das aktuell aktive MiniGame-ViewModel zurück, oder null wenn kein MiniGame aktiv.
    /// Ermöglicht ein einziges ContentControl statt 10 separate (spart ~9 View-Instanzen + Renderer).
    /// </summary>
    public BaseMiniGameViewModel? ActiveMiniGameViewModel => ActivePage switch
    {
        ActivePage.SawingGame => MiniGames.Sawing,
        ActivePage.PipePuzzle => MiniGames.PipePuzzle,
        ActivePage.WiringGame => MiniGames.Wiring,
        ActivePage.PaintingGame => MiniGames.Painting,
        ActivePage.RoofTilingGame => MiniGames.RoofTiling,
        ActivePage.BlueprintGame => MiniGames.Blueprint,
        ActivePage.DesignPuzzleGame => MiniGames.DesignPuzzle,
        ActivePage.InspectionGame => MiniGames.Inspection,
        ActivePage.ForgeGame => MiniGames.Forge,
        ActivePage.InventGame => MiniGames.Invent,
        _ => null
    };

    public bool IsAnyMiniGameActive => ActiveMiniGameViewModel != null;

    /// <summary>
    /// AAA-Audit P0 Lazy-View-Loading: Liefert das ViewModel der aktuell aktiven Seite,
    /// oder null fuer Direct-Bound-Views (Dashboard/Imperium/Missionen/Prestige), die im
    /// MainView weiter via IsVisible toggle’n.
    ///
    /// MainView nutzt ein einzelnes &lt;ContentControl Content="{Binding ActivePageContent}"/&gt;
    /// statt 25+ einzelner ContentControls — das spart bei Cold-Start die Materialisierung
    /// aller nicht-aktiven Sub-Views (~25 SkiaSharp-Renderer pro App-Start vermieden).
    /// </summary>
    public object? ActivePageContent => ActivePage switch
    {
        // Direct-Bound (kein ContentControl-Routing) — null = MainView's IsVisible-Bindings greifen.
        ActivePage.Dashboard or ActivePage.Buildings or ActivePage.Missionen or ActivePage.Prestige
            => null,

        // Top-Level-Tabs
        ActivePage.Shop => ShopViewModel,
        ActivePage.Statistics => StatisticsViewModel,
        ActivePage.Achievements => AchievementsViewModel,
        ActivePage.Settings => SettingsViewModel,
        ActivePage.WorkshopDetail => WorkshopViewModel,
        ActivePage.OrderDetail => OrderViewModel,
        ActivePage.WorkerMarket => WorkerMarketViewModel,
        ActivePage.Research => ResearchViewModel,
        ActivePage.Manager => ManagerViewModel,
        ActivePage.Tournament => TournamentViewModel,
        ActivePage.SeasonalEvent => SeasonalEventViewModel,
        ActivePage.BattlePass => BattlePassViewModel,
        ActivePage.Crafting => CraftingViewModel,
        ActivePage.Ascension => AscensionViewModel,
        ActivePage.Guild => GuildViewModel,

        // Gilden-Sub-Pages (Thin-Wrapper-VMs ueber GuildViewModel)
        ActivePage.GuildResearch => GuildViewModel.ResearchVM,
        ActivePage.GuildMembers => GuildViewModel.MembersVM,
        ActivePage.GuildInvite => GuildViewModel.InviteVM,
        ActivePage.GuildWarSeason => GuildViewModel.WarSeasonViewModel,
        ActivePage.GuildBoss => GuildViewModel.BossViewModel,
        ActivePage.GuildHall => GuildViewModel.HallViewModel,
        ActivePage.GuildAchievements => GuildViewModel.AchievementsVM,
        ActivePage.GuildChat => GuildViewModel.ChatVM,
        ActivePage.GuildWar => GuildViewModel.WarVM,

        // MiniGames (delegieren an ActiveMiniGameViewModel)
        ActivePage.SawingGame or ActivePage.PipePuzzle or ActivePage.WiringGame
            or ActivePage.PaintingGame or ActivePage.RoofTilingGame or ActivePage.BlueprintGame
            or ActivePage.DesignPuzzleGame or ActivePage.InspectionGame or ActivePage.ForgeGame
            or ActivePage.InventGame => ActiveMiniGameViewModel,

        _ => null
    };

    /// <summary>True wenn ActivePageContent ein VM liefert (= ContentControl wird sichtbar).</summary>
    public bool HasActivePageContent => ActivePageContent != null;

    // Overlay-States (überlagern die aktuelle Seite, ActivePage bleibt unverändert)
    [ObservableProperty]
    private bool _isWorkerProfileActive;

    partial void OnIsWorkerProfileActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(IsTabBarVisible));
    }

    /// <summary>
    /// Schliesst das Worker-Profile Bottom-Sheet. Wird von Backdrop-Klick + Close-Button verwendet.
    /// Ersetzt den Umweg ueber HandleBackPressed() (Code-Behind-Anti-Pattern).
    /// </summary>
    [RelayCommand]
    private void CloseWorkerProfile() => IsWorkerProfileActive = false;

    [ObservableProperty]
    private bool _isLuckySpinVisible;

    /// <summary>Turnier-Button sichtbar ab Level 50 (Progressive Disclosure).</summary>
    [ObservableProperty]
    private bool _showTournamentSection;

    /// <summary>Saison-Event-Button sichtbar ab Level 60.</summary>
    [ObservableProperty]
    private bool _showSeasonalEventSection;

    /// <summary>Battle-Pass-Button sichtbar ab Level 70.</summary>
    [ObservableProperty]
    private bool _showBattlePassSection;

    // ═══════════════════════════════════════════════════════════════════════
    // PROGRESSIVE TAB-FREISCHALTUNG
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Minimales Spieler-Level pro Tab-Index (0=Werkstatt, 1=Imperium, 2=Missionen, 3=Gilde, 4=Shop).
    /// Zentralisiert in <see cref="LevelThresholds"/>.
    /// </summary>
    public static int[] TabUnlockLevels => LevelThresholds.TabUnlockLevels;

    /// <summary>
    /// Gibt zurück ob der Tab bei gegebenem Index für das aktuelle Level gesperrt ist.
    /// </summary>
    public bool IsTabLocked(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= TabUnlockLevels.Length) return false;
        // Nach dem ersten Prestige alle Tabs permanent freigeschaltet
        if (HasEverPrestiged) return false;
        return HeaderVM.PlayerLevel < TabUnlockLevels[tabIndex];
    }

    /// <summary>
    /// Gibt ein gecachtes Array zurück das angibt welche Tabs gesperrt sind (für Tab-Bar-Renderer).
    /// Cache wird nur bei Level-Änderung invalidiert (statt 25x/s neu allokiert).
    /// </summary>
    private bool[]? _cachedLockedTabs;
    private int _lockedTabsCacheLevel = -1;

    public bool[] GetLockedTabs()
    {
        if (_cachedLockedTabs != null && _lockedTabsCacheLevel == HeaderVM.PlayerLevel)
            return _cachedLockedTabs;

        _cachedLockedTabs = new bool[5];
        for (int i = 0; i < 5; i++)
            _cachedLockedTabs[i] = IsTabLocked(i);
        _lockedTabsCacheLevel = HeaderVM.PlayerLevel;
        return _cachedLockedTabs;
    }

    /// <summary>
    /// Haupt-Tabs bei denen die Tab-Bar sichtbar ist (5 Hauptseiten).
    /// </summary>
    // P1.5 AAA-Audit: s_mainTabs wandert als statisches Set in den Helper.

    /// <summary>
    /// Tab-Bar sichtbar nur auf den 5 Haupt-Tabs und wenn kein Overlay aktiv ist.
    /// </summary>
    public bool IsTabBarVisible => Helpers.PageNavigationHelper.MainTabs.Contains(ActivePage) && !IsWorkerProfileActive;

    /// <summary>
    /// NAV-3: Breadcrumb-Text für Sub-Views (zeigt den Parent-Tab wenn Tab-Bar versteckt ist).
    /// </summary>
    public string BreadcrumbText => ActivePage switch
    {
        ActivePage.WorkshopDetail or ActivePage.OrderDetail or
        ActivePage.SawingGame or ActivePage.PipePuzzle or ActivePage.WiringGame or
        ActivePage.PaintingGame or ActivePage.RoofTilingGame or ActivePage.BlueprintGame or
        ActivePage.DesignPuzzleGame or ActivePage.InspectionGame or ActivePage.ForgeGame or
        ActivePage.InventGame => _localizationService.GetString("TabWorkshop") ?? "Workshop",
        ActivePage.WorkerMarket or ActivePage.Research or ActivePage.Manager or
        ActivePage.Crafting or ActivePage.Ascension => _localizationService.GetString("TabImperium") ?? "Empire",
        ActivePage.Tournament or ActivePage.SeasonalEvent or ActivePage.BattlePass or
        ActivePage.Statistics or ActivePage.Achievements => _localizationService.GetString("TabMissions") ?? "Missions",
        ActivePage.GuildResearch or ActivePage.GuildMembers or ActivePage.GuildInvite or
        ActivePage.GuildWarSeason or ActivePage.GuildBoss or ActivePage.GuildHall or
        ActivePage.GuildAchievements or ActivePage.GuildChat or ActivePage.GuildWar => _localizationService.GetString("TabGuild") ?? "Guild",
        ActivePage.Settings => _localizationService.GetString("Settings") ?? "Settings",
        _ => ""
    };

    // P1.5 AAA-Audit: ActivePagePropertyName ist als Helper extrahiert
    // (Helpers/PageNavigationHelper.GetPropertyNameFor). 41 Zeilen weniger im MainViewModel.

}
