using HandwerkerImperium.Models;
using HandwerkerImperium.Models.Enums;
using HandwerkerImperium.ViewModels;
using HandwerkerImperium.ViewModels.MiniGames;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Host-Facade fuer NavigationService, DialogOrchestrator und MiniGameNavigator.
/// Der MainViewModel implementiert dieses Interface explizit; die Services greifen
/// auf MainViewModel-Properties/-Methoden ausschliesslich ueber diesen Contract zu.
/// Ziel (/3): Die Logik lebt in den Services, MainViewModel haelt nur noch
/// Zustand + Child-VM-Properties. Das Host-Interface bleibt bewusst klein und
/// ist unit-testbar (Mock-Host moeglich).
/// </summary>
public interface INavigationHost
{
    // ── Seiten-Zustand ──────────────────────────────────────────────────
    ActivePage ActivePage { get; set; }
    bool IsWorkerProfileActive { get; set; }
    bool IsLuckySpinVisible { get; set; }
    bool IsCombinedWelcomeDialogVisible { get; }
    bool IsOfflineEarningsDialogVisible { get; }
    bool IsDailyRewardDialogVisible { get; set; }

    // ── Gameplay-/Service-Zugriff fuer Route-Handling ──────────────────
    bool IsQuickJobsUnlocked { get; }
    bool IsTabLocked(int tabIndex);

    // Tab-Aktivierung/Refresh (bleiben vorerst auf MainViewModel, da sie Child-VMs anwerfen)
    void SelectDashboardTab();
    void SelectBuildingsTab();
    void SelectStatisticsTab();
    void SelectAchievementsTab();
    void SelectResearchTab();
    void SelectWorkerMarketTab();
    void RefreshOrders();
    void RefreshFromState();

    // Back-Stack (Ownership bleibt MainViewModel wegen ActivePage-Seiteneffekten)
    void NavigateBackStack();

    /// <summary>
    /// Leert den Navigation-Back-Stack (v2.0.35).
    /// Wird nach Order-Completion aufgerufen damit der Spieler nach Dashboard-Sprung
    /// nicht versehentlich zum (fertigen) Auftrag zurueck-navigiert.
    /// </summary>
    void ClearNavigationStack();

    // Prestige-Kaskade (wird von Route "prestige" ausgeloest)
    void ShowPrestigeConfirmationAsyncFireAndForget();

    // Child-VM-Zugriff fuer Route-Handling
    ManagerViewModel ManagerViewModel { get; }
    TournamentViewModel TournamentViewModel { get; }
    SeasonalEventViewModel SeasonalEventViewModel { get; }
    BattlePassViewModel BattlePassViewModel { get; }
    GuildViewModel GuildViewModel { get; }
    CraftingViewModel CraftingViewModel { get; }
    AscensionViewModel AscensionViewModel { get; }
    WorkerProfileViewModel WorkerProfileViewModel { get; }
    WorkshopViewModel WorkshopViewModel { get; }
    MissionsFeatureViewModel MissionsVM { get; }
    DialogViewModel DialogVM { get; }
    MiniGameViewModels MiniGames { get; }

    // MiniGame-spezifischer Zustand (Ownership bleibt MainViewModel, aber MiniGameNavigator liest/schreibt)
    BaseMiniGameViewModel? ActiveMiniGameViewModel { get; }

    // QuickJob-State (fuer Back-Aktion aus QuickJob-MiniGame)
    QuickJob? ActiveQuickJob { get; set; }
    bool QuickJobMiniGamePlayed { get; set; }

    // Turnier-Kontext
    bool IsTournamentRound { get; set; }

    // Ad-Banner ausblenden (Worker-Profile-Overlay)
    void HideBanner();

    // Lokalisierung (fuer MiniGame-Abbruch-Dialog und Exit-Hint)
    string GetLocalizedString(string key, string fallback);

    // Offline-Earnings einsammeln (fuer Back-Press-Kaskade)
    void CollectOfflineEarningsNormal();
    void DismissCombinedDialog();
    void CheckDeferredDialogs();
    void HideLuckySpinOverlay();

    /// <summary>
    /// Prueft ob ein neues Story-Kapitel freigeschaltet wurde (v2.0.36).
    /// Wird z.B. nach QuickJob-Abschluss aufgerufen, damit Story Ch.2 sofort
    /// nach dem ersten QuickJob feuert.
    /// </summary>
    void CheckForNewStoryChapter();

    // Double-Back-to-Exit (bleibt in MainViewModel wegen BackPressHelper-Ownership)
    bool HandleDoubleBackExit();
}
