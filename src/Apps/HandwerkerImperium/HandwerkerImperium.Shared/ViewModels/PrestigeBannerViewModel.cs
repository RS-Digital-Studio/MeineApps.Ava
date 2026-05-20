using CommunityToolkit.Mvvm.ComponentModel;
using MeineApps.Core.Ava.ViewModels;

namespace HandwerkerImperium.ViewModels;

/// <summary>
/// ViewModel fuer das Prestige-Banner (Imperium-Tab Ende). Properties fuer Tier-Verfuegbarkeit,
/// PP-Vorschau, aktive Challenges, naechste Tier-Schwelle und Speedrun-Timer.
/// Extrahiert aus MainViewModel (17.04.2026, Schritt 8).
/// Wird ueber <see cref="MainViewModel.PrestigeBannerVM"/> in den Views referenziert.
/// MainViewModel bietet Delegate-Properties fuer Rueckwaertskompatibilitaet mit bestehenden Bindings.
/// </summary>
public sealed partial class PrestigeBannerViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isPrestigeAvailable;
    [ObservableProperty] private string _prestigePointsPreview = "";
    [ObservableProperty] private string _prestigePreviewGains = "";
    [ObservableProperty] private string _prestigePreviewLosses = "";
    [ObservableProperty] private string _prestigePreviewSpeedUp = "";
    [ObservableProperty] private string _prestigePreviewTierName = "";

    /// <summary>Ob ein naechsthoeherer Prestige-Tier existiert (fuer Fortschritts-Anzeige).</summary>
    [ObservableProperty] private bool _hasNextPrestigeTier;

    /// <summary>Anzahl aktiver Challenges (fuer Badge-Anzeige im Prestige-Banner).</summary>
    [ObservableProperty] private int _activeChallengeCount;

    /// <summary>Text-Anzeige aktiver Challenges (z.B. "Spartaner +40%, Sprint +35%").</summary>
    [ObservableProperty] private string _activeChallengesText = "";

    // Challenge-Chip aktiv/inaktiv State (6 Challenges)
    [ObservableProperty] private bool _isChallengeSpartanerActive;
    [ObservableProperty] private bool _isChallengeOhneForschungActive;
    [ObservableProperty] private bool _isChallengeInflationszeitActive;
    [ObservableProperty] private bool _isChallengeSoloMeisterActive;
    [ObservableProperty] private bool _isChallengeSprintActive;
    [ObservableProperty] private bool _isChallengeKeinNetzActive;

    /// <summary>Aktuelle Run-Dauer als Text (fuer Prestige-Banner Speedrun-Anzeige).</summary>
    [ObservableProperty] private string _currentRunDuration = "";

    /// <summary>Kompakter Fortschrittstext zum naechsten Tier (z.B. "Lv. 45/100 → Silver").</summary>
    [ObservableProperty] private string _nextPrestigeTierHint = "";

    /// <summary>Fortschritt zum naechsten Tier (0.0 - 1.0).</summary>
    [ObservableProperty] private double _nextPrestigeTierProgress;

    /// <summary>
    /// F-18: Kumulierter Eternal-Mastery-Bonus aller bisherigen Prestiges (z.B. "+12.5%").
    /// Wird im Prestige-Banner neben dem Tier-Bonus angezeigt, damit Endgame-Spieler die
    /// Late-Game-Skalierung sehen.
    /// </summary>
    [ObservableProperty] private string _eternalMasteryBonusDisplay = "";

    /// <summary>Ob ueberhaupt ein Eternal-Mastery-Bonus existiert (Sichtbarkeit der Anzeige).</summary>
    [ObservableProperty] private bool _hasEternalMasteryBonus;
}
