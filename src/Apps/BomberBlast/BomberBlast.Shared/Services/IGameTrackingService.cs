using BomberBlast.Models;
using BomberBlast.Models.Entities;

namespace BomberBlast.Services;

/// <summary>
/// Wrapper-Service für alle Tracking-Aufrufe in GameEngine.
/// Kapselt IAchievementService, IWeeklyChallengeService, IDailyMissionService,
/// ICollectionService, ILeagueService, IBattlePassService und ICardService.
/// Reduziert GameEngine-Constructor von 20 auf 13 Parameter.
/// </summary>
public interface IGameTrackingService
{
    /// <summary>Karten-Service für Gameplay-Zugriff (Migration, Deck-Loading, Card-Drops)</summary>
    ICardService Cards { get; }

    /// <summary>Kumulative Kills (für Achievement-Berechnung in GameEngine)</summary>
    int TotalEnemyKills { get; }

    // --- Bomben ---
    void OnSpecialBombUsed();
    void OnPowerBombUsed();
    void OnLineBombUsed();
    void OnDetonatorUsed();
    void OnBombKicked();

    // --- Spieler ---
    void OnCurseSurvived(CurseType curseType);
    void OnComboReached(int comboCount);

    // --- Items ---
    void OnPowerUpCollected(string powerUpType);

    // --- Gegner ---
    void OnEnemyKilled(EnemyType type, bool isSurvival);
    void OnBossKilled(BossType kind, int bossFlag, float bossTime);
    void OnBossEncountered(BossType bossType);

    // --- Level ---
    void OnStoryLevelCompleted(int level, int score, int stars, int bombsUsed,
        float timeRemaining, float timeUsed, bool noDamage, int totalStars,
        bool isDailyChallenge);
    void OnWorldPerfected(int world);
    void OnQuickPlayCompleted(int difficulty);

    // --- Dungeon ---
    void OnDungeonFloorCompleted(int floor);
    void OnDungeonBossDefeated();
    void OnDungeonRunCompleted();

    // --- Survival ---
    void OnSurvivalEnded(float timeElapsed, int enemiesKilled);

    // --- Gems ---
    /// <summary>Boss-Level Erst-Abschluss: 5 Gems Belohnung (L10, L20, ..., L100)</summary>
    void OnBossLevelFirstComplete(int level);

    // --- Persistenz ---
    void FlushIfDirty();
}
