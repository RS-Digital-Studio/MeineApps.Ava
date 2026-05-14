using System.Globalization;

namespace BomberBlast.ViewModels.Navigation;

/// <summary>
/// Wandelt typsichere <see cref="NavigationRequest"/>-Records in String-Routen um
/// (Sprint 4.x AAA-Audit #7 — MainViewModel-Reduktion).
///
/// <para>
/// Reine Funktion ohne State — aus <c>MainViewModel.NavigateTo</c> +
/// <c>NavigationRequestToRoute</c> extrahiert. Die fruehere Fast-Duplikation zwischen
/// beiden Methoden ist durch die rekursive Behandlung von <see cref="GoResetThen"/>
/// eliminiert.
/// </para>
/// </summary>
public static class NavigationRouteMapper
{
    /// <summary>
    /// Liefert die String-Route fuer einen <see cref="NavigationRequest"/>.
    /// Unbekannte Requests fallen sicher auf <c>"MainMenu"</c> zurueck.
    /// </summary>
    public static string ToRoute(NavigationRequest request) => request switch
    {
        GoMainMenu => "MainMenu",
        GoLevelSelect => "LevelSelect",
        GoSettings => "Settings",
        GoShop => "Shop",
        GoAchievements => "Achievements",
        GoHighScores => "HighScores",
        GoHelp => "Help",
        GoStatistics => "Statistics",
        GoProfile => "Profile",
        GoDailyChallenge => "DailyChallenge",
        GoLuckySpin => "LuckySpin",
        GoQuickPlay => "QuickPlay",
        GoWeeklyChallenge => "WeeklyChallenge",
        GoCollection => "Collection",
        GoDeck => "Deck",
        GoDungeon => "Dungeon",
        GoBattlePass => "BattlePass",
        GoLeague => "League",
        GoGemShop => "GemShop",
        GoBossRush => "BossRush",
        GoDailyRace => "DailyRace",
        GoPlayHub => "PlayHub",
        GoBack => "..",
        GoGame g => $"Game?mode={g.Mode}&level={g.Level}&difficulty={g.Difficulty}&continue={g.Continue}&boost={g.Boost}&floor={g.Floor}&seed={g.Seed}&master={g.MasterMode}",
        GoGameOver go => $"GameOver?score={go.Score}&level={go.Level}&highscore={go.IsHighScore}&mode={go.Mode}&coins={go.Coins}&levelcomplete={go.LevelComplete}&cancontinue={go.CanContinue}&enemypts={go.EnemyPoints}&timebonus={go.TimeBonus}&effbonus={go.EfficiencyBonus}&multiplier={go.Multiplier.ToString(CultureInfo.InvariantCulture)}&kills={go.Kills}&survivaltime={go.SurvivalTime.ToString(CultureInfo.InvariantCulture)}",
        GoVictory v => $"Victory?score={v.Score}&coins={v.Coins}",
        // Kompound-Route: rekursiv — der innere Request wird selbst zur Route gemappt.
        GoResetThen r => $"//MainMenu/{ToRoute(r.Then)}",
        _ => "MainMenu",
    };
}
