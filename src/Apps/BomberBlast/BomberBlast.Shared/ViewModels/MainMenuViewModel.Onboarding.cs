using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BomberBlast.ViewModels;

/// <summary>
/// Onboarding-Partial des MainMenuViewModel (v2.0.43, Menu-Redesign).
///
/// Zeigt beim ersten Start nach dem Layout-Update ein Modal mit drei Hinweisen,
/// damit Bestand das neue Dashboard-Layout findet:
/// <list type="number">
///   <item>"Tagesaktionen findest du jetzt direkt im 'Heute'-Bereich"</item>
///   <item>"Profil, Statistiken &amp; Customize hinter deinem Avatar"</item>
///   <item>"Modi findest du als Tiles unter Story Mode"</item>
/// </list>
///
/// Persistenz via Pref-Key <see cref="OnboardingSeenPrefKey"/> — wird nach erstem
/// Schliessen gesetzt, blockiert weitere Anzeigen permanent.
/// </summary>
public sealed partial class MainMenuViewModel
{
    private const string OnboardingSeenPrefKey = "dashboard_intro_seen_v3";

    [ObservableProperty] private bool _isOnboardingVisible;
    [ObservableProperty] private string _onboardingTitle = "";
    [ObservableProperty] private string _onboardingHint1 = "";
    [ObservableProperty] private string _onboardingHint2 = "";
    [ObservableProperty] private string _onboardingHint3 = "";
    [ObservableProperty] private string _onboardingCloseText = "";

    /// <summary>
    /// Prueft beim ersten OnAppearing ob das Onboarding gezeigt werden soll.
    /// Idempotent durch Pref-Key — wird einmal pro Installation gezeigt.
    /// </summary>
    private void TryShowOnboarding()
    {
        if (_preferencesService.Get(OnboardingSeenPrefKey, false)) return;

        OnboardingTitle = _localizationService.GetString("OnboardingTitle") ?? "New Layout!";
        OnboardingHint1 = _localizationService.GetString("OnboardingHint1")
            ?? "You'll find daily activities directly in the 'Today' panel.";
        OnboardingHint2 = _localizationService.GetString("OnboardingHint2")
            ?? "Profile, statistics and customize live behind your avatar.";
        OnboardingHint3 = _localizationService.GetString("OnboardingHint3")
            ?? "Game modes are tiles under Story Mode.";
        OnboardingCloseText = _localizationService.GetString("OnboardingClose") ?? "Got it";

        IsOnboardingVisible = true;
    }

    [RelayCommand]
    private void DismissOnboarding()
    {
        IsOnboardingVisible = false;
        _preferencesService.Set(OnboardingSeenPrefKey, true);
    }
}
