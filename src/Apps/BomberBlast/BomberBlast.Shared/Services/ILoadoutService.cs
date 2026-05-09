using BomberBlast.Models.Entities;

namespace BomberBlast.Services;

/// <summary>
/// Power-Up-Loadout fuer Story-Levels (v2.0.41, Plan Task 3.2).
///
/// Spieler kann vor jedem Level 1-2 Boost-Power-Ups einsetzen — Coin/Gem-Cost direkt vor Run-Start.
/// Persistenz pro Level: Wenn der Spieler stirbt und das Level wiederholt, ist das Loadout vorausgewaehlt.
///
/// Wirkung wird in <see cref="Core.GameEngine.StartStoryModeAsync"/> via <c>ApplyLoadoutBoosts</c>
/// auf Player.MaxBombs / FireRange / SpeedLevel / HasWallpass / Invincibility appliziert.
/// </summary>
public interface ILoadoutService
{
    /// <summary>Liefert die fuer das gegebene Level gespeicherten Boosts (max 2 Eintraege).</summary>
    IReadOnlyList<LoadoutBoost> GetSavedLoadout(int level);

    /// <summary>
    /// Speichert das Loadout fuer ein Level. Aelteres Loadout wird ueberschrieben.
    /// Maximal 2 Boosts pro Level (mehr wuerde Pre-Level-Spam ermoeglichen).
    /// </summary>
    void SaveLoadout(int level, IReadOnlyList<LoadoutBoost> boosts);

    /// <summary>Loescht das gespeicherte Loadout fuer ein Level (z.B. bei Skip).</summary>
    void ClearLoadout(int level);

    /// <summary>Coin-Kosten fuer einen Boost-Typ.</summary>
    int GetCoinCost(LoadoutBoostType type);

    /// <summary>Gem-Kosten als Alternative zu Coins (5x billiger als 1.000 Coins ≈ 5 Gems).</summary>
    int GetGemCost(LoadoutBoostType type);

    /// <summary>
    /// Bezahlt die Kosten und gibt die LoadoutBoost-Liste zurueck wenn erfolgreich.
    /// Bei Fehlschlag (zu wenig Coins/Gems): keine Mutation, returnt null.
    /// </summary>
    /// <param name="level">Story-Level fuer das die Boosts gelten.</param>
    /// <param name="boosts">Auszuwaehlende Boost-Typen (max 2).</param>
    /// <param name="useGems">Wenn true, mit Gems bezahlen statt Coins.</param>
    IReadOnlyList<LoadoutBoost>? Purchase(int level, IReadOnlyList<LoadoutBoostType> boosts, bool useGems);
}

/// <summary>
/// Boost-Typen fuer das Loadout-System (v2.0.41).
/// Mappen 1:1 auf Player-Stats — alle starten als gewaehlt + bezahlt direkt vor Run-Start.
/// </summary>
public enum LoadoutBoostType
{
    /// <summary>+1 MaxBombs.</summary>
    ExtraBomb,

    /// <summary>+1 FireRange.</summary>
    ExtraFire,

    /// <summary>SpeedLevel = 3 (Maximum, statt 0 Default).</summary>
    SpeedBoost,

    /// <summary>HasWallpass = true (Wandlauf durch Bloecke).</summary>
    Wallpass,

    /// <summary>Mystery-Effekt: 30s Unverwundbarkeit beim Start.</summary>
    Invincibility,
}

/// <summary>
/// Persistierter Loadout-Eintrag (Boost-Typ + Bezahlmethode fuer UI-Anzeige).
/// </summary>
public class LoadoutBoost
{
    public LoadoutBoostType Type { get; set; }
    public bool PaidWithGems { get; set; }
}
