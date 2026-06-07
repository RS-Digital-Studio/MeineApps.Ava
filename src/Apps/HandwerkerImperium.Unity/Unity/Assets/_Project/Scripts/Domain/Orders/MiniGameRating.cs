namespace HandwerkerImperium.Domain.Orders
{
    /// <summary>
    /// Ergebnis-Bewertung der Mini-Game-Leistung.
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/MiniGameResult.cs). Reine Spiellogik —
    /// Lokalisierungs-/Farb-Methoden leben in der Unity-UI-Schicht. Numerische Werte save-relevant.
    /// </summary>
    public enum MiniGameRating
    {
        /// <summary>Zielzone komplett verfehlt</summary>
        Miss = 0,

        /// <summary>Äußere OK-Zone getroffen</summary>
        Ok = 1,

        /// <summary>Innere Good-Zone getroffen</summary>
        Good = 2,

        /// <summary>Zentrale Perfect-Zone getroffen</summary>
        Perfect = 3
    }

    /// <summary>
    /// Extension-Methoden für <see cref="MiniGameRating"/> (reine Spiellogik-Werte).
    /// </summary>
    public static class MiniGameRatingExtensions
    {
        /// <summary>Belohnungs-Prozentsatz für diese Bewertung.</summary>
        public static decimal GetRewardPercentage(this MiniGameRating rating) => rating switch
        {
            MiniGameRating.Miss => 0.20m,    // 20% der Basis-Belohnung
            MiniGameRating.Ok => 0.50m,      // 50%
            MiniGameRating.Good => 1.00m,    // 100%
            MiniGameRating.Perfect => 1.50m, // 150% (Bonus!)
            _ => 1.0m
        };

        /// <summary>XP-Prozentsatz für diese Bewertung.</summary>
        public static decimal GetXpPercentage(this MiniGameRating rating) => rating switch
        {
            MiniGameRating.Miss => 0.20m,    // 20% XP
            MiniGameRating.Ok => 0.50m,      // 50% XP
            MiniGameRating.Good => 1.00m,    // 100% XP
            MiniGameRating.Perfect => 1.50m, // 150% XP (Bonus!)
            _ => 1.0m
        };
    }
}
