using BomberBlast.Models.Entities;
using BomberBlast.Models.Grid;
using BomberBlast.Models.Levels;

namespace BomberBlast.Core.LevelGeneration;

/// <summary>
/// Extrahierte Level-Fabrik: erstellt PowerUp-Platzierung, Exit-Platzierung, Enemy/Boss-Spawning.
///
/// Ist zustandslos (Singleton). Mutiert nur das übergebene <see cref="LevelGenerationContext"/>
/// und gibt Entities zurück, die der Aufrufer in seine Listen einhängt. Keine Game-Events,
/// keine Sounds, kein Tracking — das bleibt in GameEngine.
/// </summary>
public interface ILevelGenerator
{
    /// <summary>Lokalisierter Display-Name fuer einen Level-Mutator.</summary>
    string GetMutatorDisplayName(LevelMutator mutator);

    /// <summary>
    /// Versteckt PowerUps unter Bloecken + zusaetzliche Power-Up-Luck-Boni.
    /// Mutiert <see cref="GameGrid"/>-Zellen (setzt <c>HiddenPowerUp</c>).
    /// </summary>
    void PlacePowerUps(LevelGenerationContext context);

    /// <summary>
    /// Versteckt Exit unter einem Block (klassisches Bomberman).
    /// Mutiert eine Zelle: setzt <c>HasHiddenExit = true</c> und <c>HiddenPowerUp = null</c>.
    /// </summary>
    void PlaceExit(LevelGenerationContext context);

    /// <summary>
    /// Spawnt alle Gegner (inkl. Boss/Duo-Boss) laut Level-Config.
    /// Gibt die gespawnten Entities zurueck, damit der Aufrufer Boss-Encounter tracken kann.
    /// </summary>
    List<Enemy> SpawnEnemies(LevelGenerationContext context);
}

/// <summary>
/// Input + Zustands-Handle für eine Level-Generierung.
/// Der Generator schreibt auf <see cref="Grid"/>-Zellen und liest <see cref="Level"/>.
/// </summary>
public sealed class LevelGenerationContext
{
    /// <summary>Das Grid des aktuellen Levels (wird beim Platzieren mutiert).</summary>
    public required GameGrid Grid { get; init; }

    /// <summary>Die Level-Definition mit Enemy-/PowerUp-Config.</summary>
    public required Level CurrentLevel { get; init; }

    /// <summary>Seeded Random fuer reproduzierbare Generierung.</summary>
    public required Random Random { get; init; }

    /// <summary>PowerUpLuck-Upgrade-Level des Spielers (0-2), aus ShopService.</summary>
    public int PowerUpLuckLevel { get; init; }

    /// <summary>
    /// Sprint 7.1 AAA-Audit #14: Hero-PowerUp-Drop-Multiplier (Default 1.0).
    /// Skaliert die Anzahl der vom PowerUpLuck-Upgrade gewuerfelten Extra-PowerUps —
    /// z.B. LuckyLola (1.20) gibt 20% mehr PowerUps. Default 1.0 = kein Effekt.
    /// </summary>
    public float HeroPowerUpMultiplier { get; init; } = 1.0f;

    /// <summary>
    /// Welle 1 v2.0.58 AAA-Audit #19: Hero-Block-Drop-Chance-Bonus (Default 0.0).
    /// Additiv — pro Bonus-Prozent (z.B. 0.10) wird ein zusaetzlicher Block mit einem
    /// versteckten PowerUp belegt (Round-to-Int). BrickBoris (0.10) gibt ~10% mehr PowerUps.
    /// </summary>
    public float HeroBlockDropChanceBonus { get; init; } = 0.0f;
}
