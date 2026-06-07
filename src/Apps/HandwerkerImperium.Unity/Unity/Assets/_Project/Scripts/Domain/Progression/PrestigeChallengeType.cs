using System.Collections.Generic;

namespace HandwerkerImperium.Domain.Progression
{
    /// <summary>
    /// Optionale Prestige-Herausforderungen (Run-Modifikatoren).
    /// Spieler wählen bis zu 3 Erschwerungen VOR dem Prestige für Extra-PP.
    /// Boni stacken additiv (z.B. +45% + +35% = +80% PP).
    ///
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/PrestigeChallengeType.cs). Die
    /// reinen PP-Bonus-Werte leben hier; UI-Methoden (Name-/Beschreibungs-Key, Icon)
    /// wandern in die Unity-Präsentationsschicht.
    /// </summary>
    public enum PrestigeChallengeType
    {
        /// <summary>Max 3 Worker pro Workshop → +45% PP</summary>
        Spartaner = 0,

        /// <summary>Keine Forschung möglich → +30% PP</summary>
        OhneForschung = 1,

        /// <summary>Doppelte Upgrade-Kosten → +25% PP</summary>
        Inflationszeit = 2,

        /// <summary>Nur 1 Workshop erlaubt → +50% PP</summary>
        SoloMeister = 3,

        /// <summary>Kein Offline-Einkommen → +35% PP</summary>
        Sprint = 4,

        /// <summary>Keine Lieferanten → +20% PP</summary>
        KeinNetz = 5
    }

    /// <summary>
    /// Extension-Methoden für Prestige-Challenge-Werte.
    /// </summary>
    public static class PrestigeChallengeExtensions
    {
        /// <summary>Maximale Anzahl gleichzeitig aktiver Challenges.</summary>
        public const int MaxActiveChallenges = 3;

        /// <summary>Additiver PP-Bonus für diese Challenge (z.B. 0.45 = +45%).</summary>
        public static decimal GetPpBonus(this PrestigeChallengeType challenge) => challenge switch
        {
            PrestigeChallengeType.Spartaner => 0.45m,        // näher an SoloMeister
            PrestigeChallengeType.OhneForschung => 0.30m,
            PrestigeChallengeType.Inflationszeit => 0.25m,
            PrestigeChallengeType.SoloMeister => 0.50m,    // gesenkt (dominierte die Meta)
            PrestigeChallengeType.Sprint => 0.35m,
            PrestigeChallengeType.KeinNetz => 0.20m,
            _ => 0m
        };

        /// <summary>
        /// Berechnet den additiven Gesamt-PP-Bonus für eine Liste von Challenges.
        /// Beispiel: Spartaner (+45%) + Sprint (+35%) = +80% → Multiplikator 1.80.
        /// </summary>
        public static decimal GetTotalPpMultiplier(this IReadOnlyList<PrestigeChallengeType> challenges)
        {
            if (challenges.Count == 0) return 1.0m;

            decimal totalBonus = 0m;
            for (int i = 0; i < challenges.Count; i++)
                totalBonus += challenges[i].GetPpBonus();

            return 1.0m + totalBonus;
        }
    }
}
