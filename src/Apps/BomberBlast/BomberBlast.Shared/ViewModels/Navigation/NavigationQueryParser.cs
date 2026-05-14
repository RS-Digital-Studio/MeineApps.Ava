using System.Globalization;

namespace BomberBlast.ViewModels.Navigation;

/// <summary>
/// Parst Query-String-Parameter aus Routen wie <c>"Game?mode=story&amp;level=5"</c>
/// (Sprint 4.x AAA-Audit #7 — MainViewModel-Reduktion).
///
/// <para>
/// Reine Funktionen ohne State, ViewModel-Bezug oder Seiteneffekte — aus dem
/// ~380-LOC <c>NavigateToRouteAsync</c>-Switch extrahiert. Damit ist das
/// Query-Parsing isoliert testbar (erste Test-Insel fuer MainViewModel-Navigation).
/// </para>
/// </summary>
public static class NavigationQueryParser
{
    /// <summary>Zerlegt einen Query-String (<c>"a=1&amp;b=2"</c>) in Key/Value-Paare.</summary>
    private static IEnumerable<(string Key, string Value)> Split(string query)
    {
        foreach (var param in query.Split('&'))
        {
            var parts = param.Split('=');
            if (parts.Length == 2)
                yield return (parts[0], parts[1]);
        }
    }

    private static int ParseInt(string s) => int.TryParse(s, out var v) ? v : 0;

    private static float ParseFloat(string s)
        => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

    private static bool ParseBool(string s) => bool.TryParse(s, out var v) && v;

    /// <summary>Parst die Parameter der <c>Game</c>-Route.</summary>
    public static GameRouteParams ParseGame(string query)
    {
        var p = new GameRouteParams();
        foreach (var (key, value) in Split(query))
        {
            switch (key)
            {
                case "mode": p.Mode = value; break;
                case "level": p.Level = ParseInt(value); break;
                case "difficulty": p.Difficulty = ParseInt(value); break;
                case "continue": p.Continue = ParseBool(value); break;
                case "boost": p.Boost = value; break;
                case "floor": p.Floor = ParseInt(value); break;
                case "seed": p.Seed = ParseInt(value); break;
                case "master": p.Master = ParseBool(value); break;
            }
        }
        return p;
    }

    /// <summary>Parst die Parameter der <c>GameOver</c>-Route.</summary>
    public static GameOverRouteParams ParseGameOver(string query)
    {
        var p = new GameOverRouteParams();
        foreach (var (key, value) in Split(query))
        {
            switch (key)
            {
                case "score": p.Score = ParseInt(value); break;
                case "level": p.Level = ParseInt(value); break;
                case "highscore": p.IsHighScore = ParseBool(value); break;
                case "mode": p.Mode = value; break;
                case "coins": p.Coins = ParseInt(value); break;
                case "levelcomplete": p.LevelComplete = ParseBool(value); break;
                case "cancontinue": p.CanContinue = ParseBool(value); break;
                case "enemypts": p.EnemyPoints = ParseInt(value); break;
                case "timebonus": p.TimeBonus = ParseInt(value); break;
                case "effbonus": p.EfficiencyBonus = ParseInt(value); break;
                case "multiplier": p.Multiplier = ParseFloat(value); break;
                case "kills": p.Kills = ParseInt(value); break;
                case "survivaltime": p.SurvivalTime = ParseFloat(value); break;
            }
        }
        return p;
    }

    /// <summary>Parst die Parameter der <c>Victory</c>-Route.</summary>
    public static VictoryRouteParams ParseVictory(string query)
    {
        var p = new VictoryRouteParams();
        foreach (var (key, value) in Split(query))
        {
            switch (key)
            {
                case "score": p.Score = ParseInt(value); break;
                case "coins": p.Coins = ParseInt(value); break;
            }
        }
        return p;
    }
}

/// <summary>Geparste Parameter der <c>Game</c>-Route. Defaults entsprechen <see cref="GoGame"/>.</summary>
public sealed class GameRouteParams
{
    public string Mode { get; set; } = "quick";
    public int Level { get; set; } = 1;
    public int Difficulty { get; set; } = 5;
    public bool Continue { get; set; }
    public string Boost { get; set; } = "";
    public int Floor { get; set; }
    public int Seed { get; set; }
    public bool Master { get; set; }
}

/// <summary>Geparste Parameter der <c>GameOver</c>-Route.</summary>
public sealed class GameOverRouteParams
{
    public int Score { get; set; }
    public int Level { get; set; }
    public bool IsHighScore { get; set; }
    public string Mode { get; set; } = "story";
    public int Coins { get; set; }
    public bool LevelComplete { get; set; }
    public bool CanContinue { get; set; }
    public int EnemyPoints { get; set; }
    public int TimeBonus { get; set; }
    public int EfficiencyBonus { get; set; }
    public float Multiplier { get; set; } = 1f;
    public int Kills { get; set; }
    public float SurvivalTime { get; set; }
}

/// <summary>Geparste Parameter der <c>Victory</c>-Route.</summary>
public sealed class VictoryRouteParams
{
    public int Score { get; set; }
    public int Coins { get; set; }
}
