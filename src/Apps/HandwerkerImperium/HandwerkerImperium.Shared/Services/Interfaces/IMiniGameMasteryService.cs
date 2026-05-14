using HandwerkerImperium.Models.Enums;

namespace HandwerkerImperium.Services.Interfaces;

/// <summary>
/// Verwaltet das Mini-Game-Mastery-Tier-System (v2.0.36).
///
/// Pro Mini-Game-Type werden Lifetime-Perfect-Ratings gezaehlt (NICHT zurueckgesetzt
/// bei Ascension/Prestige). Bei Erreichen der Schwellen 50/200/1000 wird das
/// Bronze/Silver/Gold-Tier freigeschaltet und 5/15/50 Goldschrauben ausgezahlt.
///
/// Das System belohnt manuelles Mini-Game-Spielen ueber den Auto-Complete-Threshold
/// (30 Perfects) hinaus — sonst werden die zehn aufwendig gebauten Mini-Games nach
/// kurzer Zeit zur Skip-Pflicht .
/// </summary>
public interface IMiniGameMasteryService
{
    /// <summary>
    /// Liefert das aktuell freigeschaltete Mastery-Tier fuer ein Mini-Game.
    /// </summary>
    MiniGameMasteryTier GetCurrentTier(MiniGameType type);

    /// <summary>
    /// Liefert die Lifetime-Perfect-Anzahl fuer ein Mini-Game.
    /// </summary>
    int GetLifetimePerfectCount(MiniGameType type);

    /// <summary>
    /// Liefert die Lifetime-Anzahl, die fuer das naechste Tier benoetigt wird,
    /// oder null wenn bereits Gold erreicht ist.
    /// </summary>
    int? GetNextTierThreshold(MiniGameType type);

    /// <summary>
    /// Wird vom RecordPerfectRating-Pfad aufgerufen — prueft, ob ein neues Tier
    /// freigeschaltet wurde, und feuert <see cref="MasteryTierUnlocked"/>.
    /// </summary>
    void OnPerfectRatingRecorded(MiniGameType type);

    /// <summary>
    /// Feuert wenn ein neues Mastery-Tier erreicht wurde. Belohnung wurde bereits
    /// gutgeschrieben, der Subscriber kann UI/Toast/Sound aufrufen.
    /// </summary>
    event EventHandler<MasteryTierUnlockedEventArgs>? MasteryTierUnlocked;
}

/// <summary>
/// Event-Args fuer <see cref="IMiniGameMasteryService.MasteryTierUnlocked"/>.
/// </summary>
public sealed class MasteryTierUnlockedEventArgs : EventArgs
{
    public required MiniGameType MiniGameType { get; init; }
    public required MiniGameMasteryTier Tier { get; init; }
    public required int GoldenScrewReward { get; init; }
}
