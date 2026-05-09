using BomberBlast.Models.Dungeon;

namespace BomberBlast.Core.Dungeon;

/// <summary>
/// Pure-Funktion zur Erkennung von 5 Dungeon-Buff-Synergien (v2.0.39+ Extract aus GameEngine.Level.cs).
///
/// Synergien sind Kombinationen aus 2 oder mehr aktiven Buffs die einen Bonus-Effekt freischalten.
/// Diese Funktion ist bewusst zustandslos und seiteneffekt-frei — sie liest die Buff-Liste und
/// liefert ein Result-Struct mit allen Synergie-Flags + kumulativer BombTimer-Reduktion.
///
/// GameEngine.ApplyDungeonBuffs ruft diese Methode einmal beim Floor-Start auf und schreibt
/// die Flags in ihre Runtime-Felder (_synergyBlitzkriegActive etc.). Das macht den Setup-Code
/// linearer und die Synergie-Tabelle isoliert dokumentiert.
/// </summary>
public static class DungeonSynergyResolver
{
    /// <summary>
    /// Wertet die aktiven Buffs gegen die 5 bekannten Synergie-Regeln aus.
    /// </summary>
    public static DungeonSynergyResult Resolve(IReadOnlyList<DungeonBuffType> buffs)
    {
        bool blitzkrieg = buffs.Contains(DungeonBuffType.SpeedBoost) && buffs.Contains(DungeonBuffType.BombTimer);
        bool fortress = buffs.Contains(DungeonBuffType.Shield) && buffs.Contains(DungeonBuffType.ExtraLife);
        bool midas = buffs.Contains(DungeonBuffType.CoinBonus) && buffs.Contains(DungeonBuffType.GoldRush);
        bool elemental = buffs.Contains(DungeonBuffType.EnemySlow) && buffs.Contains(DungeonBuffType.FireImmunity);
        bool bombardier = buffs.Contains(DungeonBuffType.ExtraBomb) && buffs.Contains(DungeonBuffType.ExtraFire);

        // Kumulative Zuendschnur-Reduktion: BombTimer-Buff + Blitzkrieg-Synergie addieren je 0.5s.
        float bombFuseReduction = 0f;
        if (buffs.Contains(DungeonBuffType.BombTimer)) bombFuseReduction += 0.5f;
        if (blitzkrieg) bombFuseReduction += 0.5f;

        return new DungeonSynergyResult
        {
            Bombardier = bombardier,
            Blitzkrieg = blitzkrieg,
            Fortress = fortress,
            Midas = midas,
            Elemental = elemental,
            BombFuseReduction = bombFuseReduction,
        };
    }
}

/// <summary>
/// Synergie-Auswertung als pure Datenstruktur. Felder werden 1:1 in GameEngine-Flags geschrieben.
/// </summary>
public readonly struct DungeonSynergyResult
{
    /// <summary>ExtraBomb + ExtraFire → nochmal +1 auf beides (sofort, kein Runtime-Flag).</summary>
    public bool Bombardier { get; init; }

    /// <summary>SpeedBoost + BombTimer → Bomben-Timer -0.5s extra.</summary>
    public bool Blitzkrieg { get; init; }

    /// <summary>Shield + ExtraLife → Shield regeneriert nach 20s ohne Schaden.</summary>
    public bool Fortress { get; init; }

    /// <summary>CoinBonus + GoldRush → Gegner droppen Mini-Coins bei Tod.</summary>
    public bool Midas { get; init; }

    /// <summary>EnemySlow + FireImmunity → Lava verlangsamt Gegner statt Spieler zu schaden.</summary>
    public bool Elemental { get; init; }

    /// <summary>Kumulative Zuendschnur-Reduktion in Sekunden (BombTimer-Buff + Blitzkrieg-Synergie).</summary>
    public float BombFuseReduction { get; init; }
}
