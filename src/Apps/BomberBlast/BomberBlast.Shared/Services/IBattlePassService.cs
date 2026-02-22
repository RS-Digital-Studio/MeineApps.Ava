using BomberBlast.Models.BattlePass;

namespace BomberBlast.Services;

/// <summary>
/// Service für den Battle Pass (30-Tage-Saisons mit Free/Premium-Track).
/// Verwaltet XP-Tracking, Tier-Fortschritt, Reward-Claims und Saison-Management.
/// </summary>
public interface IBattlePassService
{
    /// <summary>Aktueller Battle-Pass-Zustand</summary>
    BattlePassData Data { get; }

    /// <summary>Ob ein aktiver (nicht abgelaufener) Battle Pass existiert</summary>
    bool IsSeasonActive { get; }

    /// <summary>Ob der Spieler Premium hat</summary>
    bool IsPremium { get; }

    /// <summary>Aktuelles Tier (0-basiert)</summary>
    int CurrentTier { get; }

    /// <summary>Verbleibende Tage</summary>
    int DaysRemaining { get; }

    /// <summary>
    /// Fügt XP hinzu. Prüft automatisch auf Tier-Aufstiege.
    /// </summary>
    /// <param name="amount">XP-Menge</param>
    /// <param name="source">Quelle (für Logging/Feedback)</param>
    /// <returns>Anzahl der Tier-Aufstiege</returns>
    int AddXp(int amount, string source = "");

    /// <summary>
    /// Beansprucht die Belohnung eines Tiers.
    /// </summary>
    /// <param name="tierIndex">0-basierter Tier-Index</param>
    /// <param name="isPremiumReward">Ob die Premium-Belohnung beansprucht wird</param>
    /// <returns>Die beanspruchte Belohnung, oder null wenn nicht möglich</returns>
    BattlePassReward? ClaimReward(int tierIndex, bool isPremiumReward);

    /// <summary>
    /// Prüft ob eine neue Saison gestartet werden muss und startet sie ggf.
    /// </summary>
    /// <returns>true wenn neue Saison gestartet wurde</returns>
    bool CheckAndStartNewSeason();

    /// <summary>
    /// Aktiviert den Premium-Pass für die aktuelle Saison.
    /// </summary>
    void ActivatePremium();

    /// <summary>Event wenn sich der Battle Pass ändert (XP, Tier-Up, Claim)</summary>
    event Action? BattlePassChanged;

    /// <summary>Event wenn ein Tier aufgestiegen wurde (mit Tier-Nummer)</summary>
    event Action<int>? TierUpReached;
}

/// <summary>
/// XP-Quellen für den Battle Pass
/// </summary>
public static class BattlePassXpSources
{
    public const int StoryLevelComplete = 100;
    public const int ThreeStarsFirstTime = 50;
    public const int DailyChallenge = 200;
    public const int DailyMission = 80;
    public const int DailyMissionBonus = 150;
    public const int WeeklyMission = 120;
    public const int WeeklyBonus = 300;
    public const int DungeonFloor = 50;
    public const int DungeonBoss = 100;
    public const int BossKill = 200;
    public const int Survival60s = 100;
    public const int DailyLogin = 50;
    public const int LuckySpin = 30;
    public const int CollectionMilestone25 = 100;
    public const int CollectionMilestone50 = 200;
    public const int CollectionMilestone75 = 300;
    public const int CollectionMilestone100 = 500;
    public const int CardUpgrade = 80;
    public const int LeagueSeasonReward = 150;
}
