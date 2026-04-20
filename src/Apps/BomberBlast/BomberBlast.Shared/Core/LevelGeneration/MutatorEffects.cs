using BomberBlast.Models.Entities;
using BomberBlast.Models.Levels;

namespace BomberBlast.Core.LevelGeneration;

/// <summary>
/// Mutator-Effekt-Anwender (v2.0.30+ Extract aus GameEngine.Level.cs).
///
/// Zustandslos, pure Funktion. Mutiert ausschliesslich den uebergebenen Player.
/// Weder Input aus Engine-Feldern noch Seiten-Effekte (Sound, Tracking, Events).
///
/// Entscheidung: Als Static-Klasse, nicht als Interface/DI-Service. Der Gewinn
/// durch Interface waere minimal (zustandsloser Transformator ohne Dependencies),
/// der Aufwand (Mock/Setup) unverhaeltnismaessig.
/// </summary>
public static class MutatorEffects
{
    /// <summary>
    /// Wendet den Mutator-Effekt auf den Spieler an. Idempotent fuer <see cref="LevelMutator.None"/>.
    /// </summary>
    public static void Apply(Player player, LevelMutator mutator)
    {
        switch (mutator)
        {
            case LevelMutator.DoubleSpeed:
                // Spieler bewegt sich deutlich schneller (+2 Speed-Stufen).
                // Gegner-Speed wird in GameEngine.UpdateEnemies per _activeMutator-Check bearbeitet (1.5x).
                player.SpeedLevel = Math.Min(player.SpeedLevel + 2, 3);
                break;

            case LevelMutator.AllPowerBombs:
                // Jede Bombe ist eine PowerBomb (Range = Fire + MaxBombs - 1)
                player.HasPowerBomb = true;
                player.FireRange = Math.Max(player.FireRange, 4);
                break;

            case LevelMutator.NoTimer:
                // Timer wird bereits in LevelGenerator auf 99999 gesetzt (siehe Level.TimeLimit).
                break;

            // MirrorControls: Wird in Update() bei Input-Verarbeitung geprueft (_activeMutator-Check).
            // InvisibleBlocks: Wird in GameRenderer.Grid.cs bei Block-Rendering geprueft.
            // Beide: Kein Init-Effekt noetig, reine Laufzeit-Entscheidungen.
        }
    }
}
