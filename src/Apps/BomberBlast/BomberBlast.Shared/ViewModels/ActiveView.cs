namespace BomberBlast.ViewModels;

/// <summary>
/// Konsolidierte Navigation-State-Enum (v2.0.37).
/// Ersetzt die 17 IsXxxActive-Boolean-Properties durch eine einzige Enum-Property.
/// Reduziert Touchpoints pro neuer View von 5 auf 1 (Enum-Wert hinzufuegen).
/// </summary>
public enum ActiveView
{
    None,
    MainMenu,
    Game,
    LevelSelect,
    Settings,
    HighScores,
    GameOver,
    Shop,
    Victory,
    Statistics,
    QuickPlay,
    Dungeon,
    BattlePass,
    League,
    Profile,
    GemShop,
    Cards,
    Challenges,
    BossRush,
    DailyRace,
    /// <summary>.1 : "Spielen"-Tab — Hub fuer alle Spielmodi.</summary>
    PlayHub,
}
