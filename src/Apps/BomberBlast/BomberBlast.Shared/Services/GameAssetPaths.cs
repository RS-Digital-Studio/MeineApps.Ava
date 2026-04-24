using BomberBlast.Models.Entities;

namespace BomberBlast.Services;

/// <summary>
/// Zentrale Registrierung aller Asset-Pfade für AI-generierte WebP-Bitmaps.
/// Wird von Renderern und Preload-Strategien verwendet, damit die Pfad-Logik
/// nicht über mehrere Dateien verteilt ist.
///
/// <para>Struktur unter <c>Assets/visuals/</c>:
/// <list type="bullet">
///   <item><c>worlds/world_{name}.webp</c> (10 Welten)</item>
///   <item><c>enemies/enemy_{name}.webp</c> (12 Typen)</item>
///   <item><c>powerups/powerup_{name}.webp</c> (12 Typen)</item>
///   <item><c>bosses/boss_{name}.webp</c> (5 Typen - im Splash preloaded)</item>
///   <item><c>menu_bg/menu_{thema}.webp</c> (7 Themes - im Splash preloaded)</item>
/// </list>
/// </para>
/// </summary>
public static class GameAssetPaths
{
    /// <summary>Dateiname-Mapping für die 10 Welten (Index 0-9).</summary>
    public static readonly string[] WorldAssetNames =
    [
        "forest", "industrial", "cavern", "sky", "inferno",
        "ruins", "ocean", "volcano", "sky_fortress", "shadow_realm"
    ];

    /// <summary>
    /// Asset-Pfad für einen Welt-Hintergrund (Index 0-9).
    /// </summary>
    public static string GetWorldAssetPath(int worldIndex)
    {
        if (worldIndex < 0 || worldIndex >= WorldAssetNames.Length) return "";
        return $"worlds/world_{WorldAssetNames[worldIndex]}.webp";
    }

    /// <summary>
    /// Asset-Pfad für einen Gegner-Typ.
    /// </summary>
    public static string GetEnemyAssetPath(EnemyType type)
    {
        return $"enemies/enemy_{type.ToString().ToLowerInvariant()}.webp";
    }

    /// <summary>
    /// Asset-Pfad für ein PowerUp. Verwendet die gleiche Snake-Case-Logik wie
    /// HelpIconRenderer.DrawPowerUp() (BombUp → bomb_up, LineBomb → line_bomb etc.).
    /// </summary>
    public static string GetPowerUpAssetPath(PowerUpType type)
    {
        var name = type switch
        {
            PowerUpType.BombUp => "bomb_up",
            PowerUpType.LineBomb => "line_bomb",
            PowerUpType.PowerBomb => "power_bomb",
            _ => type.ToString().ToLowerInvariant()
        };
        return $"powerups/powerup_{name}.webp";
    }

    /// <summary>
    /// Liefert alle Asset-Pfade für eine bestimmte Welt (Welt-Hintergrund + alle
    /// Gegner-Typen, da die Level-Generation welt-übergreifend mischt). PowerUps
    /// werden separat beim App-Start geladen (siehe <see cref="GetAllPowerUpAssets"/>).
    /// </summary>
    /// <param name="worldIndex">Welt-Index (0-9).</param>
    public static IEnumerable<string> GetWorldPreloadAssets(int worldIndex)
    {
        var worldPath = GetWorldAssetPath(worldIndex);
        if (!string.IsNullOrEmpty(worldPath))
            yield return worldPath;

        // Alle Gegner-Typen — ein Level kann bis zu 4 verschiedene enthalten, aber
        // der LevelGenerator mischt welt-abhängig. Preloading aller 12 Typen ist
        // billig (je ~40KB WebP → ~480KB für alle) und vermeidet Frame-Drop beim
        // Erscheinen eines neuen Enemy-Typs mitten im Spiel.
        foreach (var type in Enum.GetValues<EnemyType>())
            yield return GetEnemyAssetPath(type);
    }

    /// <summary>
    /// Alle 12 PowerUp-Assets (universal, welt-übergreifend). Beim Splash preladen,
    /// damit das erste PowerUp-Spawn im Tutorial nicht per Fallback rendert.
    /// </summary>
    public static IEnumerable<string> GetAllPowerUpAssets()
    {
        foreach (var type in Enum.GetValues<PowerUpType>())
            yield return GetPowerUpAssetPath(type);
    }

    /// <summary>
    /// Alle 12 Enemy-Assets — universelles Preload (günstig, verhindert jeden
    /// Welt-Wechsel-Jank). Kann bei Bedarf welt-spezifisch limitiert werden.
    /// </summary>
    public static IEnumerable<string> GetAllEnemyAssets()
    {
        foreach (var type in Enum.GetValues<EnemyType>())
            yield return GetEnemyAssetPath(type);
    }
}
