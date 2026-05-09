namespace BomberBlast.Core.Modes;

// ═══════════════════════════════════════════════════════════════════════════
// MODE-PLUGIN-FRAMEWORK PHASE 2 (v2.0.49 — AAA-Audit Phase 6)
// ═══════════════════════════════════════════════════════════════════════════
// 8 konkrete IGameMode-Implementierungen — jede kapselt den Mode-spezifischen
// State + bietet eindeutigen ModeTag für Telemetrie / Crashlytics-Custom-Keys.
//
// Aktueller Stand: Defensive Migration. Mode-Klassen werden parallel zu den
// existierenden Bool-Flags in GameEngine.cs gepflegt. Tatsächliche Mode-Logic
// wird in Folge-Iterationen (Phase 7+) aus der Engine in die Mode-Klassen
// verschoben.

/// <summary>
/// Basis-Klasse für IGameMode-Implementierungen. Default-Methoden sind no-op,
/// damit konkrete Modi nur die relevanten Hooks überschreiben müssen.
/// </summary>
public abstract class GameModeBase : IGameMode
{
    public abstract string ModeTag { get; }
    public virtual void Initialize(GameModeContext ctx) { }
    public virtual void UpdateLogic(float deltaTime, GameModeContext ctx) { }
    public virtual bool OnLevelComplete(GameModeContext ctx) => true;
    public virtual void OnGameOver(GameModeContext ctx) { }
    public virtual void Cleanup(GameModeContext ctx) { }
}

/// <summary>
/// Standard-Story-Modus: 100 Level mit Welt-Progression, Boss-Levels alle 10, Mutator ab Welt 6.
/// </summary>
public sealed class StoryMode : GameModeBase
{
    public override string ModeTag => "story";
}

/// <summary>
/// Master-Modus: New-Game+ ab L100-Clear. Erweitert StoryMode um Speed×1.5 + Enemy-Type-Upgrade.
/// Wird parallel zu StoryMode aktiv (siehe GameEngine._isMasterMode).
/// </summary>
public sealed class MasterMode : GameModeBase
{
    public override string ModeTag => "master";
}

/// <summary>
/// Daily Challenge: Tägliches Level mit deterministischem Seed.
/// </summary>
public sealed class DailyChallengeMode : GameModeBase
{
    public override string ModeTag => "daily_challenge";
}

/// <summary>
/// Quick Play: Einzelnes Random-Level mit User-gewählter Difficulty (1-10).
/// </summary>
public sealed class QuickPlayMode : GameModeBase
{
    public int Difficulty { get; }

    public QuickPlayMode(int difficulty)
    {
        Difficulty = Math.Clamp(difficulty, 1, 10);
    }

    public override string ModeTag => "quick";
}

/// <summary>
/// Survival: Endlos-Modus mit eskalierendem Spawning.
/// Mode-spezifischer State: TimeElapsed, EnemiesKilled, SpawnTimer/SpawnInterval.
/// </summary>
public sealed class SurvivalMode : GameModeBase
{
    /// <summary>Verstrichene Zeit seit Survival-Start (Sekunden).</summary>
    public float TimeElapsed { get; set; }

    /// <summary>Survival-Spawn-Timer.</summary>
    public float SpawnTimer { get; set; }

    /// <summary>Aktuelles Spawn-Intervall (sinkt mit der Zeit).</summary>
    public float SpawnInterval { get; set; } = 4.0f;

    public override string ModeTag => "survival";
}

/// <summary>
/// Dungeon-Run: Roguelike mit Floor-1-10 + Buff-Auswahl. Persistierter Run-State im DungeonService.
/// Enthaelt Mode-spezifischen Engine-State: Legendaere Buffs (TimeFreeze/Phantom-Walk),
/// Synergie-Flags (Bombardier/Blitzkrieg/Fortress/Midas/Elemental), Floor-Modifikator-State.
/// </summary>
public sealed class DungeonMode : GameModeBase
{
    public override string ModeTag => "dungeon";

    // ─── Legendaere Buffs ───────────────────────────────────────────────────
    /// <summary>TimeFreeze: Alle Gegner eingefroren (3s bei Floor-Start).</summary>
    public float TimeFreezeTimer { get; set; }

    /// <summary>Phantom: Buff aktiv im Run.</summary>
    public bool PhantomWalkAvailable { get; set; }

    /// <summary>Phantom: Gerade durch Wände laufend.</summary>
    public bool PhantomWalkActive { get; set; }

    /// <summary>Phantom: Verbleibende Dauer (5s).</summary>
    public float PhantomWalkTimer { get; set; }

    /// <summary>Phantom: Cooldown bis nächste Aktivierung (30s).</summary>
    public float PhantomCooldownTimer { get; set; }

    /// <summary>Merkt ob Spieler echtes Wallpass hatte (vor Phantom-Aktivierung).</summary>
    public bool PlayerHadWallpassBeforePhantom { get; set; }

    // ─── Synergie-Flags ─────────────────────────────────────────────────────
    /// <summary>SpeedBoost+BombTimer: -0.5s Zünd.</summary>
    public bool SynergyBlitzkriegActive { get; set; }

    /// <summary>Shield+ExtraLife: Shield-Regen 20s.</summary>
    public bool SynergyFortressActive { get; set; }

    /// <summary>Verstrichene Zeit ohne Schaden (Fortress-Regen-Timer).</summary>
    public float FortressRegenTimer { get; set; }

    /// <summary>CoinBonus+GoldRush: Coins bei Kill.</summary>
    public bool SynergyMidasActive { get; set; }

    /// <summary>EnemySlow+FireImmunity: Lava→Slow.</summary>
    public bool SynergyElementalActive { get; set; }

    /// <summary>Kumulative Zündschnur-Reduktion (BombTimer + Blitzkrieg).</summary>
    public float DungeonBombFuseReduction { get; set; }

    /// <summary>EnemySlow Buff: 20% langsamere Gegner.</summary>
    public bool DungeonEnemySlowActive { get; set; }

    // ─── Floor-Modifikator-State ────────────────────────────────────────────
    /// <summary>Aktiver Modifikator auf diesem Floor.</summary>
    public BomberBlast.Models.Dungeon.DungeonFloorModifier FloorModifier { get; set; }

    /// <summary>Timer für Regeneration-Modifikator (Shield nach 15s).</summary>
    public float ModifierRegenTimer { get; set; }
}

/// <summary>
/// Boss-Rush: Wöchentlicher 5-Boss-Marathon mit kumuliertem Score.
/// Mode-spezifischer State: BossIndex (0-4), AccumulatedScore, TotalTimeSeconds, Submitted.
/// v2.0.54 — Phase 11: Pure-Logic-Hooks für Score-Akkumulation + Submit-Decision.
/// </summary>
public sealed class BossRushMode : GameModeBase
{
    /// <summary>Aktueller Boss-Index (0=StoneGolem, ..., 4=FinalBoss).</summary>
    public int BossIndex { get; set; }

    /// <summary>Kumulierter Score über alle 5 Bosse.</summary>
    public int AccumulatedScore { get; set; }

    /// <summary>
    /// Akkumulierte Spielzeit in Sekunden über alle bisher absolvierten Boss-Levels.
    /// Wird beim Boss-Wechsel um die echte Wall-Clock-Zeit des vorherigen Levels erhöht.
    /// </summary>
    public float TotalTimeSeconds { get; set; }

    /// <summary>Verhindert doppelte Submission an BossRushService.</summary>
    public bool Submitted { get; set; }

    public override string ModeTag => "boss_rush";

    /// <summary>
    /// Score akkumulieren + nächsten Boss-Index berechnen. Pure-Logic — testbar ohne Engine-Mocks.
    /// </summary>
    /// <param name="levelScoreEarned">Score-Differenz über das gerade gecleared Boss-Level.</param>
    /// <param name="totalBossesInSequence">Anzahl Bosse in der Sequence (typisch 5).</param>
    /// <returns>Nächster Boss-Index (≥0) oder -1 wenn alle Bosse erledigt sind.</returns>
    public int AccumulateScoreAndGetNextBossIndex(int levelScoreEarned, int totalBossesInSequence)
    {
        AccumulatedScore += levelScoreEarned;
        return BossIndex < totalBossesInSequence - 1 ? BossIndex + 1 : -1;
    }

    /// <summary>
    /// Liefert die Submit-Args wenn submitted werden soll, sonst null.
    /// Setzt Submitted=true falls true returned.
    /// </summary>
    public (int Score, float Time, bool CompletedAll)? TryGetSubmitArgs(bool completedAllBosses)
    {
        if (Submitted) return null;
        var args = (AccumulatedScore, TotalTimeSeconds, completedAllBosses);
        Submitted = true;
        return args;
    }
}

/// <summary>
/// Daily Race: Tägliches Cross-Tier-Level mit weltweitem Daily-Race-Leaderboard.
/// v2.0.54 — Phase 11: Pure-Logic-Hook für Submit-Decision.
/// </summary>
public sealed class DailyRaceMode : GameModeBase
{
    /// <summary>Verhindert doppelte Submission an LeagueService.</summary>
    public bool Submitted { get; set; }

    public override string ModeTag => "daily_race";

    /// <summary>
    /// Pre-Check + State-Mutation: returnt true wenn Score submitted werden soll, false sonst.
    /// Setzt Submitted=true falls true returned (idempotent bei Folge-Aufrufen).
    /// </summary>
    public bool TrySubmit(int finalScore)
    {
        if (Submitted || finalScore <= 0) return false;
        Submitted = true;
        return true;
    }
}
