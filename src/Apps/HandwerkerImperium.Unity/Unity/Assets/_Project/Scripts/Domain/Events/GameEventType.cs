using System;

namespace HandwerkerImperium.Domain.Events
{
    /// <summary>
    /// Typen von Zufalls- und saisonalen Events. Zufalls-Events 1-2x/Tag, saisonale sind zeitbasiert.
    /// 1:1-Port aus dem Avalonia-Original (Models/Enums/GameEventType.cs). Enum-Werte = Persistenz-Integer.
    /// UI-Extensions (GetIcon, GetLocalizationKey, GetDescriptionKey) wandern in die Präsentationsschicht.
    /// </summary>
    public enum GameEventType
    {
        // Zufalls-Events (jederzeit)
        /// <summary>Materialpreise fallen temporär (-30% Kosten).</summary>
        MaterialSale = 0,
        /// <summary>Materialknappheit, Preise steigen (+50% Kosten).</summary>
        MaterialShortage = 1,
        /// <summary>Hohe Nachfrage (+50% Auftragsbelohnungen).</summary>
        HighDemand = 2,
        /// <summary>Wirtschaftsabschwung (-30% Auftragsbelohnungen, +Reputationsgewinn).</summary>
        EconomicDownturn = 3,
        /// <summary>Steuerprüfung (10% des Geldes als Steuer abgezogen).</summary>
        TaxAudit = 4,
        /// <summary>Arbeiterstreik (Mood aller Worker sinkt).</summary>
        WorkerStrike = 5,
        /// <summary>Innovationsmesse (+30% Effizienz, +XP).</summary>
        InnovationFair = 6,
        /// <summary>Promi-Empfehlung (+Reputation, +Einkommen).</summary>
        CelebrityEndorsement = 7,

        // Saisonale Events (real-zeit-gebunden)
        /// <summary>Frühjahrs-Renovierungssaison (Mo-Fr Bonus).</summary>
        SpringSeason = 10,
        /// <summary>Sommer-Bauboom (Wochentags-Bonus).</summary>
        SummerBoom = 11,
        /// <summary>Herbst-Wartungswelle (ganze Woche Bonus).</summary>
        AutumnSurge = 12,
        /// <summary>Winter-Feiertags-Flaute (Wochenend-Malus, Wochentag normal).</summary>
        WinterSlowdown = 13
    }

    /// <summary>Gameplay-Extensions für <see cref="GameEventType"/>.</summary>
    public static class GameEventTypeExtensions
    {
        /// <summary>Ob dies ein Zufalls-Event ist (vs. saisonal).</summary>
        public static bool IsRandom(this GameEventType type) => (int)type < 10;

        /// <summary>Ob dieses Event positiv für den Spieler ist.</summary>
        public static bool IsPositive(this GameEventType type) => type switch
        {
            GameEventType.MaterialSale => true,
            GameEventType.HighDemand => true,
            GameEventType.InnovationFair => true,
            GameEventType.CelebrityEndorsement => true,
            GameEventType.SpringSeason => true,
            GameEventType.SummerBoom => true,
            GameEventType.AutumnSurge => true,
            _ => false
        };

        /// <summary>Standard-Dauer für diesen Event-Typ.</summary>
        public static TimeSpan GetDefaultDuration(this GameEventType type) => type switch
        {
            GameEventType.TaxAudit => TimeSpan.FromHours(1),
            GameEventType.WorkerStrike => TimeSpan.FromHours(2),
            GameEventType.MaterialSale => TimeSpan.FromHours(6),
            GameEventType.MaterialShortage => TimeSpan.FromHours(4),
            GameEventType.HighDemand => TimeSpan.FromHours(8),
            GameEventType.EconomicDownturn => TimeSpan.FromHours(6),
            GameEventType.InnovationFair => TimeSpan.FromHours(4),
            GameEventType.CelebrityEndorsement => TimeSpan.FromHours(8),
            _ => TimeSpan.FromHours(24) // Saisonale Events dauern 24h
        };
    }
}
