namespace BomberBlast.ViewModels;

/// <summary>
/// Typsichere Navigationsanfragen (ersetzt String-basierte Routen mit Query-Parametern).
/// </summary>
public abstract record NavigationRequest;

// ── Einfache Routen (ohne Parameter) ──
public record GoMainMenu : NavigationRequest;
public record GoLevelSelect : NavigationRequest;
public record GoSettings : NavigationRequest;
public record GoShop : NavigationRequest;
public record GoAchievements : NavigationRequest;
public record GoHighScores : NavigationRequest;
public record GoHelp : NavigationRequest;
public record GoStatistics : NavigationRequest;
public record GoProfile : NavigationRequest;
public record GoDailyChallenge : NavigationRequest;
public record GoLuckySpin : NavigationRequest;
public record GoQuickPlay : NavigationRequest;
public record GoWeeklyChallenge : NavigationRequest;
public record GoCollection : NavigationRequest;
public record GoDeck : NavigationRequest;
public record GoDungeon : NavigationRequest;
public record GoBattlePass : NavigationRequest;
public record GoLeague : NavigationRequest;
public record GoGemShop : NavigationRequest;

// ── Zurück-Navigation ──
public record GoBack : NavigationRequest;

// ── Game (komplexe Parameter) ──
public record GoGame(
    string Mode,
    int Level = 1,
    int Difficulty = 5,
    bool Continue = false,
    string Boost = "",
    int Floor = 0,
    int Seed = 0
) : NavigationRequest;

// ── GameOver (11+ Parameter) ──
public record GoGameOver(
    int Score,
    int Level,
    bool IsHighScore,
    string Mode,
    int Coins,
    bool LevelComplete,
    bool CanContinue,
    int EnemyPoints = 0,
    int TimeBonus = 0,
    int EfficiencyBonus = 0,
    float Multiplier = 1f,
    int Kills = 0,
    float SurvivalTime = 0f
) : NavigationRequest;

// ── Victory ──
public record GoVictory(int Score, int Coins) : NavigationRequest;

// ── Kompound-Route: Reset zu MainMenu, dann weiter navigieren ──
public record GoResetThen(NavigationRequest Then) : NavigationRequest;
