using BomberBlast.Models;
using BomberBlast.Models.Entities;

namespace BomberBlast.Services;

/// <summary>
/// Service für Achievements/Badges
/// </summary>
public interface IAchievementService
{
    /// <summary>Event wenn ein Achievement freigeschaltet wird</summary>
    event EventHandler<Achievement>? AchievementUnlocked;

    /// <summary>Alle Achievements</summary>
    IReadOnlyList<Achievement> Achievements { get; }

    /// <summary>Anzahl freigeschalteter Achievements</summary>
    int UnlockedCount { get; }

    /// <summary>Gesamtzahl Achievements</summary>
    int TotalCount { get; }

    /// <summary>Kumulative Gegner-Kills (für Achievement-Tracking)</summary>
    int TotalEnemyKills { get; }

    /// <summary>Kumulative Bomben-Kicks</summary>
    int TotalBombsKicked { get; }

    /// <summary>Kumulative Power-Bomben</summary>
    int TotalPowerBombs { get; }

    /// <summary>Kumulative Boss-Kills</summary>
    int TotalBossKills { get; }

    /// <summary>Kumulative Spezial-Bomben</summary>
    int TotalSpecialBombs { get; }

    /// <summary>Längste Survival-Zeit in Sekunden</summary>
    double BestSurvivalTime { get; }

    /// <summary>Anzahl Level ohne Schaden abgeschlossen</summary>
    int NoDamageLevels { get; }

    /// <summary>Anzahl Level unter 60s abgeschlossen</summary>
    int SpeedrunLevels { get; }

    /// <summary>Gesamtzahl abgeschlossener Weekly-Wochen</summary>
    int WeeklyCompletions { get; }

    /// <summary>Level abgeschlossen - prüft Fortschritts-Achievements</summary>
    Achievement? OnLevelCompleted(int level, int score, int stars, int bombsUsed, float timeRemaining, float timeUsed, bool noDamage);

    /// <summary>Gegner getötet - prüft Kampf-Achievements</summary>
    Achievement? OnEnemyKilled(int totalKills);

    /// <summary>Stern-Fortschritt aktualisieren</summary>
    Achievement? OnStarsUpdated(int totalStars);

    /// <summary>Combo erreicht - prüft Combo-Achievements</summary>
    Achievement? OnComboReached(int comboCount);

    /// <summary>Bombe gekickt - prüft Kick-Achievements</summary>
    Achievement? OnBombKicked();

    /// <summary>Power-Bomb platziert - prüft Power-Bomb-Achievements</summary>
    Achievement? OnPowerBombUsed();

    /// <summary>Curse überlebt - prüft Curse-Achievements</summary>
    Achievement? OnCurseSurvived(CurseType curseType);

    /// <summary>Daily Challenge abgeschlossen - prüft Daily-Challenge-Achievements</summary>
    Achievement? OnDailyChallengeCompleted(int totalCompleted, int currentStreak);

    /// <summary>Boss besiegt - prüft Boss-Achievements (bossTypeFlag: 1=StoneGolem, 2=IceDragon, 4=FireDemon, 8=ShadowMaster, 16=FinalBoss)</summary>
    Achievement? OnBossDefeated(int bossTypeFlag);

    /// <summary>Spezial-Bombe platziert - prüft Spezial-Bomben-Achievements</summary>
    Achievement? OnSpecialBombUsed();

    /// <summary>Survival-Runde beendet - prüft Survival-Achievements</summary>
    Achievement? OnSurvivalComplete(double survivalTime);

    /// <summary>Alle 5 Weekly-Missionen einer Woche abgeschlossen - prüft Weekly-Achievements</summary>
    Achievement? OnWeeklyWeekCompleted();

    /// <summary>Alle 30 Sterne einer Welt gesammelt - prüft Perfekt-Welt-Achievements</summary>
    Achievement? OnWorldPerfected(int world);

    /// <summary>Dungeon-Floor erreicht - prüft Dungeon-Achievements</summary>
    Achievement? OnDungeonFloorReached(int floor);

    /// <summary>Dungeon-Run abgeschlossen - prüft Run-Achievements</summary>
    Achievement? OnDungeonRunCompleted();

    /// <summary>Dungeon-Boss besiegt - prüft Dungeon-Boss-Achievement</summary>
    Achievement? OnDungeonBossDefeated();

    /// <summary>Battle-Pass-Tier erreicht - prüft BP-Achievements</summary>
    Achievement? OnBattlePassTierReached(int tier);

    /// <summary>Karte gesammelt - prüft Karten-Achievements (uniqueCount = Anzahl verschiedener Karten)</summary>
    Achievement? OnCardCollected(int uniqueCount, int maxLevel);

    /// <summary>Liga-Tier erreicht - prüft Liga-Achievements (tierIndex: 0=Bronze...4=Diamant)</summary>
    Achievement? OnLeagueTierReached(int tierIndex);

    /// <summary>Tägliche Mission abgeschlossen - prüft Daily-Mission-Achievements</summary>
    Achievement? OnDailyMissionCompleted();

    /// <summary>Survival-Kills in einem Run - prüft Kill-Achievements</summary>
    Achievement? OnSurvivalKillsReached(int kills);

    /// <summary>Line-Bomb eingesetzt - prüft Line-Bomb-Achievements</summary>
    Achievement? OnLineBombUsed();

    /// <summary>Manuelle Detonation ausgelöst - prüft Detonator-Achievements</summary>
    Achievement? OnDetonatorUsed();

    /// <summary>Glücksrad-Jackpot gewonnen - prüft Lucky-Achievements</summary>
    Achievement? OnLuckyJackpot();

    /// <summary>Quick-Play auf maximaler Schwierigkeit abgeschlossen</summary>
    Achievement? OnQuickPlayMaxCompleted();

    /// <summary>Sammlungs-Fortschritt aktualisiert - prüft Sammler-Achievements</summary>
    Achievement? OnCollectionProgressUpdated(int progressPercent);

    /// <summary>Erzwingt Speichern aller gepufferten Änderungen (Debounce-Flush)</summary>
    void FlushIfDirty();
}
