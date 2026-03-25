namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Optionale Prestige-Herausforderungen (Run-Modifikatoren).
/// Spieler wählen bis zu 3 Erschwerungen VOR dem Prestige für Extra-PP.
/// Boni stacken additiv (z.B. +40% + +30% = +70% PP).
/// </summary>
public enum PrestigeChallengeType
{
    /// <summary>Max 3 Worker pro Workshop → +40% PP</summary>
    Spartaner = 0,

    /// <summary>Keine Forschung möglich → +30% PP</summary>
    OhneForschung = 1,

    /// <summary>Doppelte Upgrade-Kosten → +25% PP</summary>
    Inflationszeit = 2,

    /// <summary>Nur 1 Workshop erlaubt → +60% PP</summary>
    SoloMeister = 3,

    /// <summary>Kein Offline-Einkommen → +35% PP</summary>
    Sprint = 4,

    /// <summary>Keine Lieferanten → +20% PP</summary>
    KeinNetz = 5
}

/// <summary>
/// Extension-Methoden für Prestige-Challenge-Werte und Lokalisierungs-Keys.
/// </summary>
public static class PrestigeChallengeExtensions
{
    /// <summary>Maximale Anzahl gleichzeitig aktiver Challenges.</summary>
    public const int MaxActiveChallenges = 3;

    /// <summary>Additiver PP-Bonus für diese Challenge (z.B. 0.40 = +40%).</summary>
    public static decimal GetPpBonus(this PrestigeChallengeType challenge) => challenge switch
    {
        PrestigeChallengeType.Spartaner => 0.40m,
        PrestigeChallengeType.OhneForschung => 0.30m,
        PrestigeChallengeType.Inflationszeit => 0.25m,
        PrestigeChallengeType.SoloMeister => 0.60m,
        PrestigeChallengeType.Sprint => 0.35m,
        PrestigeChallengeType.KeinNetz => 0.20m,
        _ => 0m
    };

    /// <summary>Lokalisierungs-Key für den Challenge-Namen.</summary>
    public static string GetNameKey(this PrestigeChallengeType challenge) => $"Challenge_{challenge}";

    /// <summary>Lokalisierungs-Key für die Challenge-Beschreibung.</summary>
    public static string GetDescriptionKey(this PrestigeChallengeType challenge) => $"Challenge_{challenge}_Desc";

    /// <summary>Icon-Name für die Challenge.</summary>
    public static string GetIcon(this PrestigeChallengeType challenge) => challenge switch
    {
        PrestigeChallengeType.Spartaner => "AccountGroup",
        PrestigeChallengeType.OhneForschung => "FlaskEmpty",
        PrestigeChallengeType.Inflationszeit => "CurrencyUsd",
        PrestigeChallengeType.SoloMeister => "Numeric1Box",
        PrestigeChallengeType.Sprint => "TimerOff",
        PrestigeChallengeType.KeinNetz => "TruckRemove",
        _ => "HelpCircle"
    };

    /// <summary>
    /// Berechnet den additiven Gesamt-PP-Bonus für eine Liste von Challenges.
    /// Beispiel: Spartaner (+40%) + Sprint (+35%) = +75% → Multiplikator 1.75.
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
