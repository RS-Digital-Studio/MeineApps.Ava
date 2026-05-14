namespace HandwerkerImperium.Models.Enums;

/// <summary>
/// Art der Lieferanten-Lieferung.
/// </summary>
public enum DeliveryType
{
    /// <summary>Geld-Bonus (basierend auf aktuellem Einkommen)</summary>
    Money,

    /// <summary>Goldschrauben (1-3 Stück)</summary>
    GoldenScrews,

    /// <summary>Erfahrungspunkte (20-100 XP)</summary>
    Experience,

    /// <summary>Stimmungs-Boost (+10 Mood für alle Worker)</summary>
    MoodBoost,

    /// <summary>Kurzer Geschwindigkeits-Boost (30min 2x Einkommen)</summary>
    SpeedBoost,

    /// <summary>
    /// V7 (): Tier-1-Material-Lieferung (1-10 Stueck eines zufaelligen
    /// Materials). Plan Section 3.5 — verdraengt Geld-Lieferung mit 25% Wahrscheinlichkeit.
    /// </summary>
    Material
}
