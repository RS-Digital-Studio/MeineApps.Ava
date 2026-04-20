using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Zentrale Navigations-API. Phase 2: Logik aus MainViewModel.OnChildNavigation und allen
/// SelectXxxTab-Methoden lebt hier.  MainViewModel behaelt nur RelayCommand-Wrapper + ActivePage-Setter
/// (wegen Side-Effects im OnActivePageChanged-Partial).
/// </summary>
public interface INavigationService
{
    /// <summary>Haengt den MainViewModel als Navigation-Host ein (zirkulaere DI-Aufloesung).</summary>
    void AttachHost(INavigationHost host);

    /// <summary>Zentraler Route-Parser (Strings aus Child-VMs + Deep-Links).</summary>
    void NavigateToRoute(string route);

    /// <summary>Direkter Sprung zu einer ActivePage (benutzt die Tab-Auswahl-Methoden).</summary>
    void NavigateTo(ActivePage page);

    /// <summary>Back-Stack-Navigation. Fallback: Dashboard.</summary>
    void NavigateBack();

    /// <summary>Tab-Auswahl mit Sicherheits-/Tutorial-Logik.</summary>
    void SelectDashboardTab();
    void SelectStatisticsTab();
    void SelectAchievementsTab();
    void SelectShopTab();
    void SelectSettingsTab();
    void SelectWorkerMarketTab();
    void SelectBuildingsTab();
    void SelectMissionenTab();
    void SelectResearchTab();
    void SelectGuildTab();
    void ShowOrdersTab();
    void ShowQuickJobsTab();
}
