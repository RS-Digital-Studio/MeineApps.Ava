namespace BomberBlast.Core.Modes;

/// <summary>
/// Mode-Plugin-Framework Phase 1 (v2.0.46 — AAA-Audit P1, siehe `BOMBERBLAST_AAA_AUDIT.md` Sektion 1.1).
///
/// AKTUELLER STAND: Skeleton-Interface. KEINE existierende Mode-Logic ist bereits umgezogen.
///
/// Begründung: Die GameEngine hat aktuell 7 Modi via Bool-Flags (`_isStoryMode`/`_isSurvivalMode`/
/// `_isQuickPlayMode`/`_isDailyChallenge`/`_isDungeonRun`/`_isBossRushMode`/`_isDailyRace`/`_isMasterMode`).
/// Jede neue Mode-Erweiterung touchiert 8-12 Stellen quer durch die Engine. Mit diesem Skeleton
/// ist der Migrations-Vertrag dokumentiert; tatsächliches Refactoring der existierenden Modi auf
/// IGameMode kommt in einem separaten Sprint (Phase 2).
///
/// Wenn ein NEUER Mode hinzukommt (z.B. "Endless Ascension"), MUSS er IGameMode implementieren —
/// statt einen weiteren Bool-Flag in GameEngine.cs anzulegen.
/// </summary>
public interface IGameMode
{
    /// <summary>Eindeutiger Mode-Tag (für Telemetrie/Crash-Custom-Keys).</summary>
    string ModeTag { get; }

    /// <summary>Wird von GameEngine beim Mode-Start aufgerufen, NACH Player-Reset und Level-Load.</summary>
    void Initialize(GameModeContext ctx);

    /// <summary>
    /// Sprint 5.x AAA-Audit #8: Wird bei jedem Level-Start aufgerufen (nach <see cref="Initialize"/>).
    /// Modi können hier level-spezifischen State zurücksetzen oder den Spawn anpassen.
    /// </summary>
    void OnLevelStart(GameModeContext ctx);

    /// <summary>
    /// Pro Frame während <see cref="GameState.Playing"/> aufgerufen.
    /// Hier kommt die mode-spezifische Logik rein (Survival-Spawn, Dungeon-Buff-Update, etc.).
    /// </summary>
    void UpdateLogic(float deltaTime, GameModeContext ctx);

    /// <summary>
    /// Sprint 5.x AAA-Audit #8: Wird gerufen wenn ein Gegner besiegt wurde (zentral in KillEnemy).
    /// Modi können z.B. eigene Spawn-Eskalation oder Reward-Logik anhängen.
    /// </summary>
    void OnEnemyKilled(GameModeContext ctx);

    /// <summary>
    /// Sprint 5.x AAA-Audit #8: Wird gerufen wenn eine Bombe explodiert (zentral in TriggerExplosion).
    /// </summary>
    void OnBombExploded(GameModeContext ctx);

    /// <summary>
    /// Sprint 5.x AAA-Audit #8: Wird gerufen wenn der Spieler getroffen wird / stirbt
    /// (zentral in KillPlayer). Modi können hier z.B. Run-Abbruch-Logik triggern.
    /// </summary>
    void OnPlayerHit(GameModeContext ctx);

    /// <summary>
    /// Sprint 5.x AAA-Audit #8: Score-Multiplikator des Modus (Default 1.0).
    /// Wird bei der zentralen Enemy-Kill-Score-Vergabe angewandt — Modi können
    /// Score skalieren ohne die Engine-Score-Logik anzufassen.
    /// </summary>
    float GetScoreModifier(GameModeContext ctx);

    /// <summary>
    /// Wird gerufen wenn Spieler ein Level erfolgreich beendet (vor Engine-Defaults).
    /// Modus kann eigene Reward-/Tracking-Logik machen.
    /// Return true = Engine soll das LevelComplete-Event feuern, false = Mode managed selbst.
    /// </summary>
    bool OnLevelComplete(GameModeContext ctx);

    /// <summary>
    /// Wird gerufen wenn Spieler stirbt und kein Continue mehr möglich ist.
    /// </summary>
    void OnGameOver(GameModeContext ctx);

    /// <summary>Wird beim Mode-Wechsel oder bei Engine-Dispose aufgerufen.</summary>
    void Cleanup(GameModeContext ctx);
}

/// <summary>
/// Engine-State-Bridge für Mode-Plugins. Liefert Zugriff auf Player, Grid, Services etc.
/// Plugins dürfen NIEMALS direkt auf GameEngine-Felder zugreifen — nur über diesen Context.
/// </summary>
public sealed class GameModeContext
{
    /// <summary>Aktueller Spieler.</summary>
    public required BomberBlast.Models.Entities.Player Player { get; init; }

    /// <summary>Grid des aktuellen Levels.</summary>
    public required BomberBlast.Models.Grid.GameGrid Grid { get; init; }

    /// <summary>Aktuelles Level-Objekt (kann null sein bei Survival/Dungeon).</summary>
    public BomberBlast.Models.Levels.Level? CurrentLevel { get; init; }

    /// <summary>Level-Nummer (1-100 in Story, 99 in Daily etc.).</summary>
    public int LevelNumber { get; init; }

    /// <summary>Vergangene Wall-Clock-Zeit seit Mode-Start.</summary>
    public required float TimeElapsed { get; init; }
}
