namespace BomberBlast.Models.Entities;

/// <summary>
/// Boss-Modifier (Sprint 6.1 AAA-Audit #15). 8 Modifier × 5 Boss-Typen = 40 Variationen.
///
/// <para>
/// Wird beim Spawn random zugewiesen (ab Welt 5+, 30% Chance — vorher None).
/// Boss-AI + Renderer werten den Modifier aus:
/// <list type="bullet">
/// <item><b>Shielded</b>: Blockt 1 Hit pro Cooldown (15s zwischen Shield-Recharge)</item>
/// <item><b>Fast</b>: +25% Bewegungsgeschwindigkeit, kuerzere Telegraph-Phase</item>
/// <item><b>Healing</b>: Regeneriert HP/s im Out-of-Combat-State</item>
/// <item><b>Summoner</b>: Spawnt 1 Mini-Enemy alle 8s</item>
/// <item><b>Frenzy</b>: Doppelter Spezial-Angriffs-Cooldown halbiert in Enrage-Phase</item>
/// <item><b>Berserk</b>: Telegraph-Dauer von 2s auf 1s reduziert (weniger Reaktionszeit)</item>
/// <item><b>Reflective</b>: Bomben-Explosionen die Boss treffen reflektieren 50% Schaden auf Spieler</item>
/// <item><b>Burning</b>: Hinterlasst Lava-Spur fuer 3s auf Bewegungspfad</item>
/// </list>
/// </para>
///
/// <para>
/// HINWEIS: Sprint 6.1 setzt nur die Foundation (Enum + Property). Die einzelnen
/// Modifier-Effekte sind separate Implementierungs-Sprints — die Spawn-Logik weist
/// einen Modifier zu, aber Effects greifen erst wenn die jeweilige Update-Logik
/// in BossEnemy.Update bzw. EnemyAI implementiert ist.
/// </para>
/// </summary>
public enum BossModifier
{
    None = 0,
    Shielded = 1,
    Fast = 2,
    Healing = 3,
    Summoner = 4,
    Frenzy = 5,
    Berserk = 6,
    Reflective = 7,
    Burning = 8,
}

/// <summary>Helper fuer zufaellige Modifier-Zuweisung beim Boss-Spawn.</summary>
public static class BossModifierExtensions
{
    private static readonly BossModifier[] AllModifiers =
    {
        BossModifier.Shielded,
        BossModifier.Fast,
        BossModifier.Healing,
        BossModifier.Summoner,
        BossModifier.Frenzy,
        BossModifier.Berserk,
        BossModifier.Reflective,
        BossModifier.Burning,
    };

    /// <summary>
    /// Liefert einen zufaelligen Modifier (oder None) basierend auf der Welt.
    /// Welt 1-4: nie Modifier (Onboarding). Welt 5-9: 30% Chance. Welt 10: 60% Chance.
    /// </summary>
    public static BossModifier RollForWorld(int worldId, Random rng)
    {
        float chance = worldId switch
        {
            <= 4 => 0f,        // Kein Modifier in Onboarding-Welten
            <= 9 => 0.30f,     // 30% Chance ab Welt 5
            _ => 0.60f,        // 60% Chance in Schattenwelt (Endgame)
        };
        if (rng.NextSingle() >= chance) return BossModifier.None;
        return AllModifiers[rng.Next(AllModifiers.Length)];
    }
}
